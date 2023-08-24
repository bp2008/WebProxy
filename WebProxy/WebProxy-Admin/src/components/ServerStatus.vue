<template>
	<div>
		<div class="connection-status" :class="{connected: isConnected}" :title="isConnected?'Connected and streaming server status.':'Not connected. Attempting to reconnect.'"></div>
		<table class="serverStatusTable">
			<thead>
				<tr>
					<th>Field</th>
					<th>Value</th>
				</tr>
			</thead>
			<tbody>
				<tr>
					<td>Is Under Heavy Load</td>
					<td>{{data.serverIsUnderHeavyLoad}}</td>
				</tr>
				<tr>
					<td>Open Connection Count</td>
					<td>{{data.serverOpenConnectionCount}}</td>
				</tr>
				<tr>
					<td>Max Connection Count</td>
					<td>{{data.serverMaxConnectionCount}}</td>
				</tr>
				<tr>
					<td>Connection Queue Count</td>
					<td>{{data.serverConnectionQueueCount}}</td>
				</tr>
				<tr>
					<td>Total Connections Accepted</td>
					<td>{{data.connectionsServed}}</td>
				</tr>
				<tr>
					<td>Total Requests Handled</td>
					<td>{{data.requestsServed}}</td>
				</tr>
				<tr>
					<td>Private Memory Size</td>
					<td>
						{{Util.formatBytesF10(data.mem_privateMemorySize).padRight(9, ' ')}}
						({{Util.formatBytes2(data.mem_privateMemorySize).padRight(10, ' ')}})
						({{data.mem_privateMemorySize}} bytes)
					</td>
				</tr>
				<tr>
					<td>Working Set</td>
					<td>
						{{Util.formatBytesF10(data.mem_workingSet).padRight(9, ' ')}}
						({{Util.formatBytes2(data.mem_workingSet).padRight(10, ' ')}})
						({{data.mem_workingSet}} bytes)
					</td>
				</tr>
				<tr>
					<td>CPU Core Usage</td>
					<td>
						{{data.cpu_coreUsagePercent}}
					</td>
				</tr>
				<tr>
					<td>CPU Time</td>
					<td>
						{{data.cpu_processorTime}}
					</td>
				</tr>
			</tbody>
		</table>
	</div>
</template>

<script>
	import * as Util from '/src/library/Util';

	export default {
		data()
		{
			return {
				data: {},
				isConnected: false,
				socket: null,
				isUnloading: false,
				Util: Util
			};
		},
		computed:
		{
		},
		methods:
		{
			connect()
			{
				if (this.isUnloading)
					return;
				console.log("ServerStatus.connect");

				let URL = location.origin + '/ServerStatus/GetServerStatusStream';
				if (URL.toLowerCase().indexOf("http:") === 0)
					URL = "ws:" + URL.substr("http:".length);
				else if (URL.toLowerCase().indexOf("https:") === 0)
					URL = "wss:" + URL.substr("https:".length);
				else
				{
					alert("Unexpected protocol. ServerStatus can't start.");
					return;
				}

				this.socket = new WebSocket(URL);

				this.socket.onopen = event =>
				{
					console.log("ServerStatus Connected");
					this.isConnected = true;
				};

				this.socket.onmessage = (event) =>
				{
					this.data = JSON.parse(event.data);
				};

				this.socket.onclose = event =>
				{
					console.log("ServerStatus Disconnected", event.code + ": " + getStatusCodeString(event.code));
					this.isConnected = false;
					setTimeout(this.connect, 1000);
				};
			},
		},
		mounted()
		{
			this.connect();
		},
		beforeUnmount()
		{
			this.isUnloading = true;
			if (this.socket)
				this.socket.close();
		},
	};

	let specificStatusCodeMappings = {
		'1000': 'Normal Closure',
		'1001': 'Going Away',
		'1002': 'Protocol Error',
		'1003': 'Unsupported Data',
		'1004': '(For future)',
		'1005': 'No Status Received',
		'1006': 'Abnormal Closure',
		'1007': 'Invalid frame payload data',
		'1008': 'Policy Violation',
		'1009': 'Message too big',
		'1010': 'Missing Extension',
		'1011': 'Internal Error',
		'1012': 'Service Restart',
		'1013': 'Try Again Later',
		'1014': 'Bad Gateway',
		'1015': 'TLS Handshake'
	};

	function getStatusCodeString(code)
	{
		if (code >= 0 && code <= 999)
			return '(Unused)';
		else if (code >= 1016)
		{
			if (code <= 1999)
				return '(For WebSocket standard)';
			else if (code <= 2999)
				return '(For WebSocket extensions)';
			else if (code <= 3999)
				return '(For libraries and frameworks)';
			else if (code <= 4999)
				return '(For applications)';
		}
		if (typeof (specificStatusCodeMappings[code]) !== 'undefined')
			return specificStatusCodeMappings[code];
		return '(Unknown)';
	}
</script>

<style scoped>
	.serverStatusTable
	{
		border-collapse: collapse;
	}

		.serverStatusTable td
		{
			border: 1px solid currentColor;
			padding: 2px 4px;
		}

		.serverStatusTable td:nth-child(2)
		{
			font-family: consolas, monospace;
			white-space: pre-wrap;
		}

	.connection-status
	{
		width: 16px;
		height: 16px;
		border-radius: 50%;
	}

		.connection-status.connected
		{
			background-color: green;
		}

		.connection-status:not(.connected)
		{
			background-color: red;
		}
</style>

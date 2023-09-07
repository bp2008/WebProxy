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
					<td>Total Connections Accepted</td>
					<td>{{data.connectionsServed}}</td>
				</tr>
				<tr>
					<td>Total Requests Handled</td>
					<td>{{data.requestsServed}}</td>
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
				<tr title="Current overall system memory load" v-if="data.ramSize !== 0">
					<td>System Memory Total</td>
					<td>
						{{Util.formatBytes2(data.ramSize)}}
					</td>
				</tr>
				<tr>
					<td>Private Memory Size</td>
					<td>
						{{Util.formatBytes2(data.mem_privateMemorySize).padRight(10, ' ')}}
						({{Util.formatBytesF10(data.mem_privateMemorySize).padRight(9, ' ')}})
						({{data.mem_privateMemorySize}} bytes)
					</td>
				</tr>
				<tr>
					<td>Working Set</td>
					<td>
						{{Util.formatBytes2(data.mem_workingSet).padRight(10, ' ')}}
						({{Util.formatBytesF10(data.mem_workingSet).padRight(9, ' ')}})
						({{data.mem_workingSet}} bytes)
					</td>
				</tr>
				<tr>
					<td>Memory Usage Breakdown<br />As of Previous GC</td>
					<td>
						<canvas width="160" height="160" ref="memCanvas"></canvas>
						<div class="legend">
							<div class="legendRow" v-for="item in pieData">
								<div class="legendBox" v-bind:style="{ backgroundColor: item.color }"></div> {{item.name}} ({{Util.formatBytes2(item.value)}})
							</div>
						</div>
					</td>
				</tr>
				<tr>
					<td>Garbage Collection Count</td>
					<td>
						{{data.gc.Index}}
					</td>
				</tr>
				<tr>
					<td>Garbage Collector Mode</td>
					<td>
						{{data.isServerGc ? "Server" : "Workstation" }}
					</td>
				</tr>
				<tr>
					<td>GC Heap Fragmentation</td>
					<td>
						{{Math.round((data.gc.FragmentedBytes / data.gc.HeapSizeBytes) * 100)}}% ({{Util.formatBytes2(data.gc.FragmentedBytes)}} / {{Util.formatBytes2(data.gc.HeapSizeBytes)}})
					</td>
				</tr>
				<tr title="Overall system memory load when the last garbage collection occurred.">
					<td>GC System Memory Load</td>
					<td>
						{{Util.formatBytes2(data.gc.MemoryLoadBytes)}}
					</td>
				</tr>
				<tr title="The high memory load threshold when the last garbage collection occurred. (when total system memory usage reaches this point, it is considered a high memory load)">
					<td>GC High Load Threshold</td>
					<td>
						{{Util.formatBytes2(data.gc.HighMemoryLoadThresholdBytes)}} {{ data.gc.MemoryLoadBytes >= data.gc.HighMemoryLoadThresholdBytes ? "(GC in High Load Mode)" : "" }}
					</td>
				</tr>
				<tr title="The total available memory, in bytes, for the garbage collector to use when the last garbage collection occurred.">
					<td>GC Available Memory</td>
					<td>
						{{Util.formatBytes2(data.gc.TotalAvailableMemoryBytes)}}
					</td>
				</tr>
				<tr>
					<td>GCMemoryInfo Raw</td>
					<td>
						{{data.gc}}
					</td>
				</tr>
				<tr title="Busy/Max [Min, Free]">
					<td>Dotnet ThreadPool</td>
					<td>
						<div>Threads:</div>
						<div>{{data.maxThreads-data.availableThreads}}/{{data.maxThreads}} (Min: {{data.minThreads}}, Free: {{data.availableThreads}})</div>
						<div>Completion Port Threads:</div>
						<div>{{data.maxCompletionPortThreads-data.availableCompletionPortThreads}}/{{data.maxCompletionPortThreads}} (Min: {{data.minCompletionPortThreads}}, Free: {{data.availableCompletionPortThreads}})</div>
					</td>
				</tr>
				<tr>
					<td>Active Connections</td>
					<td>
						<div style="min-height: 100vh" v-if="data.activeConnections">
							<div class="activeConnection" v-for="c in data.activeConnections" :key="c.ID">
								<div class="header" title="[[Connection ID]] [Client IP] -> [Host][:Port] [Num Requests Completed This Connection]">[{{c.ID}}] {{c.ClientIP}} -> {{c.Host}}{{getPortStr(c)}}{{c.Tls ? " (TLS)" : ""}} R#{{c.RequestsHandled}}</div>
								<div class="request">
									<!-- Line 1 -->
									Request:
									<template v-if="c.Request">
										{{c.Request.Method}} {{c.Request.Url}}
									</template>
									<template v-else>
										<ClockLoader class="loadingAnimation" />
									</template>
									<div class="requestBody">
										<!-- Line 2 -->
										<template v-if="c.Request && c.Request.Body">
											<template v-if="c.Request.Body.Type === 'WebSocket'">
												WebSocket
												<ClockLoader class="loadingAnimation" />
											</template>
											<template v-else>
												{{typeof c.Request.Body.Read !== 'undefined' ? c.Request.Body.Read : '~'}}/{{typeof c.Request.Body.Size !== 'undefined' ? c.Request.Body.Size : '∞'}}
												<ClockLoader class="loadingAnimation" v-if="c.Request.Body.Read !== c.Request.Body.Size" />
											</template>
										</template>
										<template v-else>
											<template v-if="c.Response">
												[no request body]
											</template>
											<template v-else>
												...
											</template>
										</template>
									</div>
								</div>
								<div class="response">
									<!-- Line 3 -->
									Response:
									<template v-if="c.Response">
										<template v-if="c.Response.Type === 'Pending'">
											Pending
										</template>
										<template v-else>
											{{c.Response.Status}}
										</template>
									</template>
									<template v-else>
										<ClockLoader class="loadingAnimation" />
									</template>
									<div class="responseBody">
										<!-- Line 4 -->
										<template v-if="c.Response">
											<template v-if="c.Response.Type === 'Pending'">
												...
											</template>
											<template v-else-if="c.Response.Type === 'WebSocket'">
												WebSocket
												<ClockLoader class="loadingAnimation" />
											</template>
											<template v-else-if="c.Response.Body">
												{{typeof c.Response.Body.Written !== 'undefined' ? c.Response.Body.Written : '~'}}/{{typeof c.Response.Body.Size !== 'undefined' ? c.Response.Body.Size : '~'}}
												<ClockLoader class="loadingAnimation" v-if="c.Response.Body.Written !== c.Response.Body.Size" />
											</template>
											<template v-else>
												Finished
											</template>
										</template>
										<template v-else>
											...
										</template>
									</div>
								</div>
							</div>
						</div>
					</td>
				</tr>
			</tbody>
		</table>
	</div>
</template>

<script>
	import * as Util from '/src/library/Util';
	import ClockLoader from '/src/assets/clockLoader.svg?component';

	export default {
		components: { ClockLoader },
		data()
		{
			return {
				data: { gc: {} },
				pieData: [],
				isConnected: false,
				socket: null,
				isUnloading: false,
				Util: Util
			};
		},
		computed:
		{
			gcIndex()
			{
				return this.data && this.data.gc ? this.data.gc.Index : -1;
			}
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
			computePieData()
			{
				let data = this.data;
				let pieData = [];
				AddPieData(pieData, "Managed Heap", data.gc.HeapSizeBytes, "#008800");
				AddPieData(pieData, "Committed Non-Heap", data.gc.TotalCommittedBytes - data.gc.HeapSizeBytes, "#FFFF00");
				AddPieData(pieData, "Unmanaged", data.mem_workingSet - data.gc.TotalCommittedBytes, "#CCCCCC");

				if (this.$refs.memCanvas)
					DrawPieChart(this.$refs.memCanvas, pieData);

				this.pieData = pieData;
			},
			getPortStr(c)
			{
				if (c.Https && c.LocalPort === 443)
					return "";
				if (!c.Https && c.LocalPort === 80)
					return "";
				return ":" + c.LocalPort;
			}
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
		watch:
		{
			gcIndex()
			{
				this.computePieData();
			}
		}
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

	var AddPieData = function (arr, name, value, color)
	{
		arr.push({ name, value, color });
	};
	var DrawPieChart = function (canvas, data)
	{
		var ctx = canvas.getContext("2d");
		var lastend = 0;
		var borderWidth = 3;
		var radius = canvas.height / 2 - borderWidth;

		var myTotal = 0;
		for (var i = 0; i < data.length; i++)
			myTotal += data[i].value;

		for (var i = 0; i < data.length; i++)
		{
			ctx.fillStyle = data[i].color;
			ctx.beginPath();
			ctx.moveTo(canvas.width / 2, canvas.height / 2);
			ctx.arc(canvas.width / 2, canvas.height / 2, radius, lastend, lastend + (Math.PI * 2 * (data[i].value / myTotal)), false);
			ctx.lineTo(canvas.width / 2, canvas.height / 2);
			ctx.fill();
			lastend += Math.PI * 2 * (data[i].value / myTotal);
		}

		ctx.strokeStyle = "white";
		ctx.lineWidth = borderWidth;
		ctx.beginPath();
		ctx.arc(canvas.width / 2, canvas.height / 2, radius, 0, Math.PI * 2);
		ctx.stroke();
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
			word-break: break-word;
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

	.legend
	{
		display: inline-block;
		border: 1px solid #888888;
		padding: 4px 8px;
		vertical-align: top;
		margin-left: 10px;
	}

	.legendBox
	{
		display: inline-block;
		width: 14px;
		height: 14px;
		border: 1px solid #888888;
		vertical-align: middle;
	}

	.activeConnection
	{
		border: 1px solid #888888;
		margin-bottom: 5px;
		white-space: pre-line;
	}

		.activeConnection:last-child
		{
			margin-bottom: 0px;
		}

		.activeConnection .header
		{
			background-color: rgba(0,0,150,0.8);
			padding: 1px 2px 3px 2px;
			color: #FFFFFF;
		}

		.activeConnection .request
		{
			margin-left: 10px;
		}

		.activeConnection .requestBody
		{
			margin-left: 10px;
		}

		.activeConnection .response
		{
			margin-left: 10px;
		}

		.activeConnection .responseBody
		{
			margin-left: 10px;
		}

	.loadingAnimation
	{
		width: 16px;
		height: 16px;
		vertical-align: text-top;
	}
</style>

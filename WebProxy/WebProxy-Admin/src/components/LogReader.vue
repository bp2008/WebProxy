<template>
	<div>
		<div class="log-box">
			<div class="connection-status" :class="{connected: isConnected}" :title="isConnected?'Connected and streaming text from the log.':'Not connected. Attempting to reconnect.'"></div>
			<pre ref="logBox">{{ logText }}</pre>
		</div>
	</div>
</template>

<script>
	export default {
		data()
		{
			return {
				logText: '',
				isConnected: false,
				socket: null,
				isScrolledToBottom: false
			};
		},
		methods:
		{
			connect()
			{
				console.debug("LogReader.connect");

				let URL = location.origin + '/Log/GetLogData';
				if (URL.toLowerCase().indexOf("http:") === 0)
					URL = "ws:" + URL.substr("http:".length);
				else if (URL.toLowerCase().indexOf("https:") === 0)
					URL = "wss:" + URL.substr("https:".length);
				else
				{
					alert("Unexpected protocol. LogReader can't start.");
					return;
				}

				this.socket = new WebSocket(URL);

				this.socket.onopen = event =>
				{
					console.log("LogReader Connected");
					this.logText = '';
					this.isConnected = true;
				};

				this.socket.onmessage = (event) =>
				{
					if (this.$refs.logBox)
					{
						// Check if within 20px of bottom
						this.isScrolledToBottom = this.$refs.logBox.scrollHeight - (this.$refs.logBox.scrollTop + this.$refs.logBox.clientHeight) < 20;
					}
					this.logText += event.data + '\n';
				};

				this.socket.onclose = event =>
				{
					console.log("LogReader Disconnected", event.code + ": " + getStatusCodeString(event.code));
					this.isConnected = false;
					setTimeout(this.connect, 1000);
				};
			},
			scrollToBottom()
			{
				this.$nextTick(() =>
				{
					if (this.$refs.logBox)
						this.$refs.logBox.scrollTop = this.$refs.logBox.scrollHeight;
				});
			}
		},
		watch:
		{
			logText(newVal, oldVal)
			{
				if (this.$refs.logBox && this.isScrolledToBottom)
					this.scrollToBottom();
			}
		},
		mounted()
		{
			this.connect();
		},
		beforeUnmount()
		{
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

<style>
	.log-box
	{
		position: relative;
		margin-bottom: 1em;
	}

		.log-box pre
		{
			border: 1px solid black;
			height: 90vh;
			white-space: pre-wrap;
			overflow-x: hidden;
			overflow-y: scroll;
		}

	.connection-status
	{
		position: absolute;
		top: 5px;
		right: 22px;
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

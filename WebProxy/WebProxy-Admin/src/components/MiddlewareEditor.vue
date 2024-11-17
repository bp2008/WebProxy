<template>
	<PrimaryContainer class="middlewareEditor" @delete="$emit('delete')" title="Middleware" :name="middleware.Id">
		<div class="flexRow">
			<label>Name</label>
			<input type="text" v-model="middleware.Id" autocomplete="off" />
			<div class="comment" v-if="store.showHelp">You can change the Middleware Name after creation, but you must manually update all Entrypoints and Exitpoints that used it.</div>
		</div>
		<div class="flexRow">
			<label>Type</label>
			<select v-model="middleware.Type">
				<option v-for="middlewareType in availableMiddlewareTypes">{{middlewareType}}</option>
			</select>
		</div>
		<template v-if="middleware.Type === 'IPWhitelist'">
			<div>
				Provides IP whitelisting of client connections to Entrypoints and/or Exitpoints where this middleware is enabled.
				<ul>
					<li>If the client IP is not on the whitelist, their connection is dropped.</li>
					<li>Multiple IPWhitelist middlewares can apply to the same client connection, in which case the client IP only needs to be on one of the whitelists.</li>
					<li>IP Whitelists are optional: If no IPWhitelist middleware is enabled for the client connection, the connection is allowed regardless of IP address.</li>
					<li>The client IP tested against whitelist(s) is the true remote IP address.  Headers such as <span class="icode">X-Forwarded-For</span> are ignored.</li>
				</ul>
			</div>
			<div v-if="!middleware.WhitelistedIpRanges || !middleware.WhitelistedIpRanges.length">
				Click the <b>+</b> button below to add an IP range.
			</div>
			<div class="ipRangeList">
				<label>Whitelisted IP Ranges</label>
				<ArrayEditor v-model="middleware.WhitelistedIpRanges" arrayType="text" />
			</div>
			<template v-if="store.showHelp">
				<div class="exampleText">Examples:</div>
				<div class="exampleText"><span class="icode">192.168.0.100</span> (single IP address)</div>
				<div class="exampleText"><span class="icode">192.168.0.90 - 192.168.0.110</span> (IP range)</div>
				<div class="exampleText"><span class="icode">192.168.0.100/30</span> (subnet prefix notation)</div>
			</template>
		</template>
		<template v-if="middleware.Type === 'HttpDigestAuth'">
			<div>
				This middleware causes requests to require HTTP Digest Authentication.
			</div>
			<div v-if="!middleware.AuthCredentials || !middleware.AuthCredentials.length">
				Click the <b>+</b> button below to add a credential.
			</div>
			<div>
				<label>Credentials</label>
				<ArrayEditor v-model="middleware.AuthCredentials" arrayType="credentials" />
			</div>
		</template>
		<template v-if="middleware.Type === 'RedirectHttpToHttps'">
			<div>
				Requests using HTTP are automatically redirected to HTTPS on the best supported Entrypoint.  This middleware only applies to requests that arrive using plain unencrypted HTTP on an Entrypoint that supports both HTTP and HTTPS.
			</div>
		</template>
		<template v-if="middleware.Type === 'AddHttpHeaderToRequest'">
			<div>
				This middleware adds or removes HTTP request headers when sending requests to the destination origin.  Only affects Exitpoints of type WebProxy.
			</div>
		</template>
		<template v-if="middleware.Type === 'AddHttpHeaderToResponse'">
			<div>
				This middleware adds or removes HTTP response headers when sending responses to the client.  Only affects Exitpoints of type WebProxy.
			</div>
		</template>
		<template v-if="middleware.Type === 'AddHttpHeaderToRequest' || middleware.Type === 'AddHttpHeaderToResponse'">
			<div class="httpHeaderList">
				<label>Http Headers</label>
				<ArrayEditor v-model="middleware.HttpHeaders" arrayType="text" />
				<div class="exampleText" v-if="store.showHelp">
					<br />
					To add a header, enter the header name and value, separated by a colon:<br />
					<span class="icode">Strict-Transport-Security: max-age=31536000; includeSubDomains</span><br />
					<br />
					To remove a header, just provide the header name with no colon:<br />
					<span class="icode">Strict-Transport-Security</span><br />
					<br />
					The following macro strings will be replaced with a dynamic value when proxying:<br />
					<br />
					<table>
						<thead><tr><th>Macro</th><th>Expands To</th></tr></thead>
						<tbody>
							<tr>
								<td><span class="icode">$remote_addr</span></td>
								<td>The IP address of the requesting client.</td>
							</tr>
							<tr>
								<td><span class="icode">$remote_port</span></td>
								<td>The remote port number of the requesting client.</td>
							</tr>
							<tr>
								<td><span class="icode">$request_proto</span></td>
								<td>The protocol used by the client to contact this server (<span class="icode">http</span> or <span class="icode">https</span>).</td>
							</tr>
							<tr>
								<td><span class="icode">$server_name</span></td>
								<td>The hostname which the client used in their request.</td>
							</tr>
							<tr>
								<td><span class="icode">$server_port</span></td>
								<td>The port number which the client connected to on this server.</td>
							</tr>
						</tbody>
					</table>
				</div>
			</div>
		</template>
		<template v-if="middleware.Type === 'AddProxyServerTiming'">
			<div>
				This middleware adds a "Server-Timing" HTTP header to all responses.  Only affects Exitpoints of type WebProxy.
			</div>
		</template>
		<template v-if="middleware.Type === 'XForwardedFor' || middleware.Type === 'XForwardedHost' || middleware.Type === 'XForwardedProto' || middleware.Type === 'XRealIp'">
			<div>
				This middleware manipulates the <span class="icode">{{proxyHeaderName}}</span> HTTP header in all outgoing requests.  Only affects Exitpoints of type WebProxy.
			</div>
			<div class="flexRow">
				<label><span class="icode">{{proxyHeaderName}}</span> Behavior</label>
				<select v-model="middleware.ProxyHeaderBehavior">
					<option v-for="proxyHeaderBehaviorOption in availableProxyHeaderBehaviorOptions">{{proxyHeaderBehaviorOption}}</option>
				</select>
			</div>
			<div v-if="proxyHeaderBehaviorDescription">
				{{proxyHeaderBehaviorDescription}}
			</div>
		</template>
		<template v-if="middleware.Type === 'TrustedProxyIPRanges'">
			<div>
				This middleware allows you to provide a list of trusted proxy IP Ranges as a requirement to properly use some proxy header behaviors.
			</div>

			<div class="ipRangeList">
				<label>Trusted Proxy IP Ranges</label>
				<ArrayEditor v-model="middleware.WhitelistedIpRanges" arrayType="text" />
			</div>
			<template v-if="store.showHelp">
				<div class="exampleText">Examples:</div>
				<div class="exampleText"><span class="icode">192.168.0.100</span> (single IP address)</div>
				<div class="exampleText"><span class="icode">192.168.0.90 - 192.168.0.110</span> (IP range)</div>
				<div class="exampleText"><span class="icode">192.168.0.100/30</span> (subnet prefix notation)</div>
			</template>
		</template>
		<template v-if="middleware.Type === 'HostnameSubstitution'">
			<div>
				This middleware performs hostname substitution on proxied responses.  Specific hostnames found in text-based responses will be replaced with the values defined below.  Only affects Exitpoints of type WebProxy.
			</div>
			<div v-if="!middleware.AuthCredentials || !middleware.AuthCredentials.length">
				Click the <b>+</b> button below to add a hostname substitution.
			</div>
			<div>
				<label>Hostname Substitutions</label>
				<ArrayEditor v-model="middleware.HostnameSubstitutions" arrayType="keyvaluepair_string_string" />
				<template v-if="store.showHelp">
					<div class="exampleText">Enter on the left a hostname which you want to remove, and on the right the hostname which you want to insert in its place.</div>
				</template>
			</div>
		</template>
		<template v-if="middleware.Type === 'RegexReplaceInResponse'">
			<div>
				This middleware performs text replacement on proxied responses using Regular Expressions.  Only affects Exitpoints of type WebProxy.
			</div>
			<div v-if="!middleware.AuthCredentials || !middleware.AuthCredentials.length">
				Click the <b>+</b> button below to add Regular Expression patterns and replacement strings.
			</div>
			<div>
				<label>Regular Expression Patterns and Replacement Strings</label>
				<ArrayEditor v-model="middleware.RegexReplacements" arrayType="keyvaluepair_string_string" />
				<template v-if="store.showHelp">
					<div class="exampleText">Case-sensitivity: You can use <span class="icode">(?i)</span> within the pattern to begin case-insensitive matching and <span class="icode">(?-i)</span> to end it. For example, <span class="icode">(?i)foo(?-i)bar</span> matches <span class="icode">FOObar</span> but not <span class="icode">fooBAR</span>. If you want the whole pattern to be case-insensitive then you don't need to include <span class="icode">(?-i)</span>.</div>
				</template>
			</div>
		</template>
	</PrimaryContainer>
</template>
<script>
	import store from '/src/library/store';
	import ArrayEditor from '/src/components/ArrayEditor.vue';
	import PrimaryContainer from '/src/components/PrimaryContainer.vue';

	export default {
		components: { ArrayEditor, PrimaryContainer },
		props: {
			middleware: Object
		},
		data()
		{
			return {
				store
			};
		},
		created()
		{
		},
		computed:
		{
			availableMiddlewareTypes()
			{
				return store.middlewareTypes;
			},
			availableProxyHeaderBehaviorOptions()
			{
				if (this.middleware.Type === "XForwardedFor")
					return store.proxyHeaderBehaviorOptions;
				else
					return store.proxyHeaderBehaviorOptions.filter(o => o.indexOf("Combine") === -1);
			},
			proxyHeaderName()
			{
				switch (this.middleware.Type)
				{
					case "XForwardedFor":
						return "X-Forwarded-For";
					case "XForwardedHost":
						return "X-Forwarded-Host";
					case "XForwardedProto":
						return "X-Forwarded-Proto";
					case "XRealIp":
						return "X-Real-Ip";
					default:
						return undefined;
				}
			},
			proxyHeaderBehaviorDescription()
			{
				return store.proxyHeaderBehaviorOptionsDescriptions[this.middleware.ProxyHeaderBehavior];
			}
		},
		methods:
		{
		},
		watch:
		{
		}
	};
</script>
<style>
	/* unscoped */
	.ipRangeList input[type="text"],
	.httpHeaderList input[type="text"]
	{
		width: 550px;
		max-width: 100%;
		box-sizing: border-box;
	}
</style>
<style scoped>
</style>

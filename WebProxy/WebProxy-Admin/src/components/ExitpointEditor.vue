<template>
	<PrimaryContainer class="exitpointEditor" @delete="$emit('delete')" title="Exitpoint" :name="exitpoint.name">
		<div class="flexRow">
			<label><b>Name</b></label>
			<input type="text" v-model="exitpoint.name" class="nameInput" autocomplete="off" />
			<div class="comment" v-if="store.showHelp">You can change the Exitpoint Name after creation, but you must manually update all affected ProxyRoutes.</div>
		</div>
		<div class="flexRow">
			<label>Exitpoint Type</label>
			<select v-model="exitpoint.type">
				<option v-for="exitpointType in availableExitpointTypes">{{exitpointType}}</option>
			</select>
		</div>
		<template v-if="exitpoint.type !== 'Disabled'">
			<div class="flexRow" :title="hostTitle">
				<label>Host Binding</label>
				<input type="text" v-model="exitpoint.host" class="hostInput" autocomplete="off" />
				<div class="exampleText" v-if="store.showHelp">Enter the hostname which should route to this exitpoint. <span class="icode">*</span> to bind to all hostnames.  Exitpoints with more-specific host bindings will take precedence over wildcard bindings.</div>
				<div class="exampleText" v-if="store.showHelp">Multiple hostnames? Use comma and/or space to separate: <span class="icode">example.com www.example.com</span>.</div>
				<div class="exampleText" v-if="store.showHelp">Wildcards are available (<span class="icode">*.example.com</span>), but will prevent LetsEncrypt certificate creation unless you have DNS validation configured and enabled.</div>
			</div>
			<template v-if="exitpoint.type === 'WebProxy'">
				<div class="flexRow" title="Destination origin, e.g. http://example.com or https://example.com:8000">
					<label>Destination Origin</label>
					<input type="text" v-model="exitpoint.destinationOrigin" class="destinationOriginInput" autocomplete="off" />
					<div class="comment" v-if="store.showHelp">Requests shall be proxied to this origin (scheme, host, optional port), e.g. <span class="icode">https://example.com:8000</span>.  If you include a request path, query string, etc, it will be accepted but ignored.</div>
				</div>
				<div class="flexRow" v-if="destinationOriginIsHttps">
					<label><input type="checkbox" v-model="exitpoint.proxyAcceptAnyCertificate" /> Skip Certificate Validation for Destination Origin</label>
				</div>
				<div class="flexRow">
					<label>Destination Host Header</label>
					<input type="text" v-model="exitpoint.destinationHostHeader" autocomplete="off" />
					<div class="comment" v-if="store.showHelp">If you need to override the host string used in outgoing proxy requests (for the Host header and TLS Server Name Indication), provide the host string here.  Otherwise leave this empty and the host from the Destination Origin will be used. DO NOT include a port number, even if using a non-standard port.  The port number will be added automatically where appropriate.</div>
				</div>
				<div class="flexRow">
					<label><input type="checkbox" v-model="exitpoint.useConnectionKeepAlive" /> Use <span class="icode">Connection: keep-alive</span></label>
					<div class="comment" v-if="store.showHelp">Enabled by default for performance reasons, you can disable this to force a new connection to be made to the destination server for every request.</div>
				</div>
				<div class="flexRow">
					<label>Connect Timeout (seconds)</label>
					<input type="number" v-model="exitpoint.connectTimeoutSec" min="1" max="60" autocomplete="off" />
					<div class="comment" v-if="store.showHelp">
						The connection timeout, in seconds.  Min: 1, Max: 60, Default: 10.<br />
						This timeout applies only to the Connect operation (when connecting to the destination server to faciliate proxying).
					</div>
				</div>
				<div class="flexRow">
					<label>Network Timeout (seconds)</label>
					<input type="number" v-model="exitpoint.networkTimeoutSec" min="1" max="600" autocomplete="off" />
					<div class="comment" v-if="store.showHelp">
						The send and receive timeout for other time-sensitive network operations, in seconds.  Min: 1, Max: 600, Default: 15.<br />
						This timeout applies to:<br />
						<ul>
							<li>Reading the HTTP request body from the client.</li>
							<li>Reading the HTTP response header from the destination server.</li>
							<li>All other proxy operations that send data on a network socket.</li>
						</ul>
						If a destination sometimes has slow time-to-first-byte, you may need to increase this timeout.<br />
						This timeout does not apply when reading a response body or websocket data because these actions often sit idle for extended periods of time.
					</div>
				</div>
			</template>
			<div class="dashedBorder">
				<div>
					<label><input type="checkbox" v-model="exitpoint.autoCertificate" /> Automatic Certificate from LetsEncrypt</label>
					<div class="comment" v-if="store.showHelp">If enabled, certificates for this host will be obtained and managed automatically via LetsEncrypt.  Automatic certificate management will only work if this exitpoint is mapped to an entrypoint that is reachable on the internet at 'http://host:80/' or 'https://host:443/' or can be validated via a configured DNS service.  Wildcards are not allowed in [Host Binding] when using this option, unless you have enabled DNS validation.</div>
				</div>
				<div v-if="!exitpoint.autoCertificate">
					<label><input type="checkbox" v-model="exitpoint.allowGenerateSelfSignedCertificate" /> Generate Self-Signed Certificate if Missing</label>
					<div class="comment" v-if="store.showHelp">If enabled, and the certificate does not exist, a self-signed certificate will be created automatically.</div>
				</div>
				<template v-if="exitpoint.autoCertificate">
					<div v-if="store.cloudflareApiToken">
						<label><input type="checkbox" v-model="exitpoint.cloudflareDnsValidation" />Use DNS-01 Validation (via Cloudflare)</label>
						<div class="comment" v-if="store.showHelp">If enabled, domain validation will be performed if possible via the <span class="icode">DNS-01</span> method using your configured Cloudflare API Token.  <span class="icode">DNS-01</span> validation does not require the WebProxy service to be accessible on any public port.</div>
					</div>
					<div>
						<input type="button" value="Force Renew Certificate" @click="$emit('renew')" />
					</div>
				</template>
				<div class="flexRow" v-if="!exitpoint.autoCertificate">
					<label>Certificate Path</label>
					<input type="text" v-model="exitpoint.certificatePath" class="certificatePathInput" placeholder="Path to the certificate file (pfx)" title="Path to the certificate file (pfx)" autocomplete="off" />
					<UploadFileControl label="Upload Certificate" acceptFileExtension=".pfx" @upload="uploadCertClicked" />
					<div class="comment" v-if="store.showHelp">Path to the certificate file (pfx). If omitted, a path will be automatically assigned upon first use.</div>
				</div>
			</div>
			<div class="middlewares">
				<MiddlewareSelector v-model="exitpoint.middlewares"></MiddlewareSelector>
			</div>
		</template>
	</PrimaryContainer>
</template>

<script>
	import MiddlewareSelector from './MiddlewareSelector.vue';
	import store from '/src/library/store';
	import UploadFileControl from '/src/components/UploadFileControl.vue';
	import PrimaryContainer from '/src/components/PrimaryContainer.vue';

	export default {
		components: { MiddlewareSelector, UploadFileControl, PrimaryContainer },
		props: {
			exitpoint: Object,
			allMiddlewares: {
				type: Array,
				default: []
			}
		},
		data()
		{
			return {
				store,
				hostTitle: "DNS hostname template.\n\nIn order to access this Exitpoint, a client must request a host which matches this template.\n\nNull or empty string will make this Exitpoint be unreachable by a standard HTTP client.\n\nAny number of '*' characters can be used as wildcards where each '*' means 0 or more characters.  Wildcard matches are lower priority than exact host matches.",
				expanded: true
			};
		},
		created()
		{
			this.expanded = false;
		},
		computed:
		{
			availableExitpointTypes()
			{
				return store.exitpointTypes;
			},
			destinationOriginIsHttps()
			{
				return this.exitpoint && this.exitpoint.destinationOrigin && this.exitpoint.destinationOrigin.toLowerCase().indexOf("https:") === 0;
			},

		},
		methods:
		{
			async uploadCertClicked(selectedFileBase64)
			{
				this.$emit('uploadCert', { exitpoint: this.exitpoint, certificateBase64: selectedFileBase64 });
			}
		},
		watch:
		{
		}
	};
</script>

<style scoped>
</style>

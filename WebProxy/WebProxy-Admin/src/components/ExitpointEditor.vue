<template>
	<div class="exitpointEditor primaryContainer">
		<FloatingButtons @delete="$emit('delete')" />
		<div class="flexRow">
			<label><b>Exitpoint Name</b></label>
			<input type="text" v-model="exitpoint.name" class="nameInput" />
			<div class="comment">You can change the Exitpoint Name after creation, but you must manually update all affected ProxyRoutes.</div>
		</div>
		<div class="flexRow">
			<label>Exitpoint Type</label>
			<select v-model="exitpoint.type">
				<option v-for="exitpointType in availableExitpointTypes">{{exitpointType}}</option>
			</select>
		</div>
		<template v-if="exitpoint.type !== 'Disabled'">
			<div class="flexRow" :title="hostTitle">
				<label>Host</label>
				<input type="text" v-model="exitpoint.host" class="hostInput" />
				<div class="exampleText">Multiple hosts? Use comma and/or space to separate: <span class="icode">example.com www.example.com</span>.</div>
				<div class="exampleText">Using wildcards will disable automatic certificate creation: <span class="icode">*.example.com</span>.</div>
			</div>
			<div class="dashedBorder">
				<div>
					<label><input type="checkbox" v-model="exitpoint.autoCertificate" /> Automatic Certificate from LetsEncrypt</label>
					<div class="comment">If enabled, certificates for this host will be obtained and managed automatically via LetsEncrypt.  Automatic certificate management will only work if this exitpoint is mapped to an entrypoint that is reachable on the internet at 'http://host:80/' or 'https://host:443/'.  Wildcards are not allowed in [Host] when using this option.</div>
				</div>
				<div><input type="button" v-if="exitpoint.autoCertificate" value="Force Renew Certificate" @click="$emit('renew')" /></div>
				<div class="flexRow" v-if="!exitpoint.autoCertificate">
					<label>Certificate Path</label>
					<input type="text" v-model="exitpoint.certificatePath" class="certificatePathInput" placeholder="Path to the certificate file (pfx)" title="Path to the certificate file (pfx)" />
					<div class="comment">Path to the certificate file (pfx). If omitted, a path will be automatically filled in upon first use.</div>
				</div>
			</div>
			<template v-if="exitpoint.type === 'WebProxy'">
				<div class="flexRow">
					<label>Destination Origin</label>
					<input type="text" v-model="exitpoint.destinationOrigin" class="destinationOriginInput" />
					<div class="comment">Requests shall be proxied to this origin, e.g. <span class="icode">https://example.com:8000</span></div>
				</div>
				<div class="flexRow" v-if="destinationOriginIsHttps">
					<label><input type="checkbox" v-model="exitpoint.proxyAcceptAnyCertificate" /> Skip Certificate Validation for Destination Origin</label>
				</div>
				<div class="flexRow">
					<label>Destination Host Header</label>
					<input type="text" v-model="exitpoint.destinationHostHeader" />
					<div class="comment">If you need to override the host string used in outgoing proxy requests (for the Host header and TLS Server Name Indication), provide the host string here.  Otherwise leave this empty and the host from the Destination Origin will be used. DO NOT include a port number.</div>
				</div>
			</template>
			<div class="middlewares">
				<MiddlewareSelector v-model="exitpoint.middlewares"></MiddlewareSelector>
			</div>
		</template>
	</div>
</template>

<script>
	import MiddlewareSelector from './MiddlewareSelector.vue';
	import store from '/src/library/store';
	import FloatingButtons from '/src/components/FloatingButtons.vue'

	export default {
		components: { MiddlewareSelector, FloatingButtons },
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
				hostTitle: "DNS hostname template.\n\nIn order to access this Exitpoint, a client must request a host which matches this template.\n\nNull or empty string will make this Exitpoint be unreachable by a standard HTTP client.\n\nAny number of '*' characters can be used as wildcards where each '*' means 0 or more characters.  Wildcard matches are lower priority than exact host matches."
			};
		},
		created()
		{
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
			}
		},
		watch:
		{
		}
	};
</script>

<style scoped>
	.exitpointEditor
	{
		background-color: #FFF8F0;
	}
</style>

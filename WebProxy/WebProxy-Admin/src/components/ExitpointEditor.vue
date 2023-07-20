<template>
	<div class="exitpointEditor primaryContainer">
		<FloatingButtons @delete="$emit('delete')" />
		<div class="flexRow">
			<label><b>Exitpoint Name</b></label>
			<input type="text" v-model="exitpoint.name" class="nameInput" />
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
					<label title="If true, certificates for this host will be obtained and managed automatically via LetsEncrypt.  Automatic certificate management will only work if this host is mapped to an http entrypoint that is reachable on the internet at 'http://host:80/'.  Wildcards are not allowed in [Host] when using this option.">
						<input type="checkbox" v-model="exitpoint.autoCertificate" /> Automatic Certificate from LetsEncrypt
					</label>
				</div>
				<div class="flexRow">
					<label>Certificate Path</label>
					<input type="text" v-model="exitpoint.certificatePath" class="certificatePathInput" title="Path to the certificate file (pfx).  If null or empty, a default path will be automatically assigned to this field." />
				</div>
			</div>
			<template v-if="exitpoint.type === 'WebProxy'">
				<div class="flexRow" title="Requests shall be proxied to this origin, e.g. https://example.com:8000">
					<label>Destination Origin</label>
					<input type="text" v-model="exitpoint.destinationOrigin" class="destinationOriginInput" />
				</div>
				<div class="exampleText">e.g. <span class="icode">https://example.com:8000</span></div>
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
		watch:
		{
		},
		computed:
		{
			availableExitpointTypes()
			{
				return store.exitpointTypes;
			}
		}
	};
</script>

<style scoped>
	.exitpointEditor
	{
		background-color: #FFF8F0;
	}
</style>

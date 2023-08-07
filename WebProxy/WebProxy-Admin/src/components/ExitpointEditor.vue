<template>
	<div class="exitpointEditor primaryContainer">
		<FloatingButtons @delete="$emit('delete')" />
		<div class="primaryContainerHeading">Exitpoint</div>
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
				<label>Host</label>
				<input type="text" v-model="exitpoint.host" class="hostInput" autocomplete="off" />
				<div class="exampleText" v-if="store.showHelp">Multiple hosts? Use comma and/or space to separate: <span class="icode">example.com www.example.com</span>.</div>
				<div class="exampleText" v-if="store.showHelp">Using wildcards will disable automatic certificate creation: <span class="icode">*.example.com</span>.</div>
			</div>
			<div class="dashedBorder">
				<div>
					<label><input type="checkbox" v-model="exitpoint.autoCertificate" /> Automatic Certificate from LetsEncrypt</label>
					<div class="comment" v-if="store.showHelp">If enabled, certificates for this host will be obtained and managed automatically via LetsEncrypt.  Automatic certificate management will only work if this exitpoint is mapped to an entrypoint that is reachable on the internet at 'http://host:80/' or 'https://host:443/' or can be validated via a configured DNS service.  Wildcards are not allowed in [Host] when using this option.</div>
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
					<div>
						<div class="uploadCertInput button">
							Upload Certificate <input ref="fileInput" type="file" accept=".pfx" @change="fileInputChangeCounter++" />
						</div>
						<span v-if="selectedFile" class="selectedFileName">{{selectedFile.name}}</span>
						<div v-if="selectedFile" class="uploadCertBtn button" @click="uploadCertClicked()">Upload <UploadIcon /></div>
					</div>
					<div class="comment" v-if="store.showHelp">Path to the certificate file (pfx). If omitted, a path will be automatically filled in upon first use.</div>
				</div>
			</div>
			<template v-if="exitpoint.type === 'WebProxy'">
				<div class="flexRow">
					<label>Destination Origin</label>
					<input type="text" v-model="exitpoint.destinationOrigin" class="destinationOriginInput" autocomplete="off" />
					<div class="comment" v-if="store.showHelp">Requests shall be proxied to this origin, e.g. <span class="icode">https://example.com:8000</span></div>
				</div>
				<div class="flexRow" v-if="destinationOriginIsHttps">
					<label><input type="checkbox" v-model="exitpoint.proxyAcceptAnyCertificate" /> Skip Certificate Validation for Destination Origin</label>
				</div>
				<div class="flexRow">
					<label>Destination Host Header</label>
					<input type="text" v-model="exitpoint.destinationHostHeader" autocomplete="off" />
					<div class="comment" v-if="store.showHelp">If you need to override the host string used in outgoing proxy requests (for the Host header and TLS Server Name Indication), provide the host string here.  Otherwise leave this empty and the host from the Destination Origin will be used. DO NOT include a port number, even if using a non-standard port.  The port number will be added automatically where appropriate.</div>
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
	import UploadIcon from '/src/assets/upload.svg?component'

	export default {
		components: { MiddlewareSelector, FloatingButtons, UploadIcon },
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
				fileInputChangeCounter: 0,
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
			},
			selectedFile()
			{
				if (this.fileInputChangeCounter > 0 && this.$refs.fileInput.files.length > 0)
					return this.$refs.fileInput.files[0];
				return null;
			},
		},
		methods:
		{
			async uploadCertClicked()
			{
				if (this.selectedFile)
				{
					if (!this.selectedFile.name.match(/\.pfx$/i))
					{
						toaster.error('File extension is not ".pfx"');
						return;
					}
					let certificateArrayBuffer = await this.selectedFile.arrayBuffer();
					let certificateBase64 = _arrayBufferToBase64(certificateArrayBuffer);
					this.$emit('uploadCert', { exitpoint: this.exitpoint, certificateBase64 });
				}
				else
					toaster.error('No file is selected');
			}
		},
		watch:
		{
		}
	};
	function _arrayBufferToBase64(buffer)
	{
		var binary = '';
		var bytes = new Uint8Array(buffer);
		var len = bytes.byteLength;
		for (var i = 0; i < len; i++)
			binary += String.fromCharCode(bytes[i]);
		return window.btoa(binary);
	}
</script>

<style scoped>
	.uploadCertInput
	{
		position: relative;
		display: inline-block;
	}

	.selectedFileName
	{
		padding: 0px 10px;
	}

	.uploadCertBtn
	{
		position: relative;
		fill: currentColor;
		display: inline-flex;
		align-items: center;
	}

		.uploadCertBtn svg
		{
			width: 20px;
			height: 20px;
			margin-left: 5px;
		}

	input[type="file"]
	{
		position: absolute;
		left: 0;
		opacity: 0;
		top: 0;
		bottom: 0;
		width: 100%;
		cursor: pointer;
	}
</style>

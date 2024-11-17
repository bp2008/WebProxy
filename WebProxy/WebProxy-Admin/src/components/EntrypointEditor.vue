<template>
	<PrimaryContainer class="entrypointEditor" @delete="$emit('delete')" title="Entrypoint" :name="entrypoint.name">
		<div class="flexRow">
			<label>Name</label>
			<input type="text" v-model="entrypoint.name" autocomplete="off" />
			<div class="comment" v-if="store.showHelp">You can change the Entrypoint Name after creation, but you must manually update all affected ProxyRoutes.</div>
		</div>
		<div class="flexRow">
			<label>IP Address Binding</label>
			<input type="text" v-model="entrypoint.ipAddress" class="ipAddressInput" placeholder="(when empty) listen on all interfaces" title="(when empty) listen on all interfaces" autocomplete="off" />
			<div class="comment" v-if="store.showHelp">IP Address to listen on.  Leave empty to listen on all interfaces.</div>
		</div>
		<div>
			<label><input type="checkbox" v-model="httpPortEnabled" /> HTTP Port: </label>
			<input type="number" min="1" max="65535" v-model="entrypoint.httpPort" :disabled="!httpPortEnabled" autocomplete="off" />
		</div>
		<div>
			<label><input type="checkbox" v-model="httpsPortEnabled" /> HTTPS Port: </label>
			<input type="number" min="1" max="65535" v-model="entrypoint.httpsPort" :disabled="!httpsPortEnabled" autocomplete="off" />
		</div>
		<div>
			<div class="comment" v-if="store.showHelp">HTTP and HTTPS can share the same port, if you like.</div>
		</div>
		<div v-if="httpsPortEnabled">
			<template v-if="store.tlsCipherSuitesPolicySupported">
				<label>TLS Cipher Suites: </label>
				<select v-model="entrypoint.tlsCipherSuiteSet">
					<option v-for="cipherSet in store.tlsCipherSuiteSets">{{cipherSet}}</option>
				</select>
				<div class="comment" v-if="store.showHelp">Useful for meeting some cybersecurity requirements.</div>
			</template>
			<template v-else>
				<div>Allowed TLS Cipher Suites are determined by the operating system configuration.</div>
				<div class="comment" v-if="store.showHelp">The platform WebProxy is running on does not support configuring TLS cipher suites at runtime.</div>
			</template>
		</div>
		<div class="comment" v-if="store.showHelp && httpsPortEnabled && store.tlsCipherSuitesPolicySupported && tlsCipherSuiteSetDescription">
			{{tlsCipherSuiteSetDescription}}
		</div>
		<div class="middlewares">
			<MiddlewareSelector v-model="entrypoint.middlewares"></MiddlewareSelector>
		</div>
	</PrimaryContainer>
</template>
<script>
	import MiddlewareSelector from './MiddlewareSelector.vue';
	import PrimaryContainer from '/src/components/PrimaryContainer.vue';
	import store from '/src/library/store';
	export default {
		components: { MiddlewareSelector, PrimaryContainer },
		props: {
			entrypoint: Object
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
			httpPortEnabled:
			{
				get()
				{
					return this.entrypoint.httpPort > 0;
				},
				set(newValue)
				{
					if (this.entrypoint.httpPort === null || isNaN(this.entrypoint.httpPort) || this.entrypoint.httpPort === 0)
						this.entrypoint.httpPort = 80;
					if (this.entrypoint.httpPort > 0 !== newValue)
						this.entrypoint.httpPort *= -1;
				}
			},
			httpsPortEnabled:
			{
				get()
				{
					return this.entrypoint.httpsPort > 0;
				},
				set(newValue)
				{
					if (this.entrypoint.httpsPort === null || isNaN(this.entrypoint.httpsPort) || this.entrypoint.httpsPort === 0)
						this.entrypoint.httpsPort = 443;
					if (this.entrypoint.httpsPort > 0 !== newValue)
						this.entrypoint.httpsPort *= -1;
				}
			},
			tlsCipherSuiteSetDescription()
			{
				return store.tlsCipherSuiteSetDescriptions[this.entrypoint.tlsCipherSuiteSet];
			}
		}
	};
</script>
<style scoped>
	.flexRow input.ipAddressInput
	{
		width: 300px;
		max-width: 100%;
		box-sizing: border-box;
	}
</style>

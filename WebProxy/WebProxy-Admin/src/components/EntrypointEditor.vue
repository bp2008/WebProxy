<template>
	<div class="entrypointEditor primaryContainer">
		<FloatingButtons @delete="$emit('delete')" />
		<div class="flexRow">
			<label>Entrypoint Name</label>
			<input type="text" v-model="entrypoint.name" />
		</div>
		<div class="flexRow">
			<label>IP Address Binding</label>
			<input type="text" v-model="entrypoint.ipAddress" class="ipAddressInput" placeholder="(when empty) listen on all interfaces" title="(when empty) listen on all interfaces" />
		</div>
		<div>
			<label><input type="checkbox" v-model="httpPortEnabled" /> HTTP Port: </label>
			<input type="number" min="1" max="65535" v-model="entrypoint.httpPort" :disabled="!httpPortEnabled" />
		</div>
		<div>
			<label><input type="checkbox" v-model="httpsPortEnabled" /> HTTPS Port: </label>
			<input type="number" min="1" max="65535" v-model="entrypoint.httpsPort" :disabled="!httpsPortEnabled" />
		</div>
		<div class="middlewares">
			<MiddlewareSelector v-model="entrypoint.middlewares"></MiddlewareSelector>
		</div>
	</div>
</template>

<script>
	import MiddlewareSelector from './MiddlewareSelector.vue';
	import FloatingButtons from '/src/components/FloatingButtons.vue'

	export default {
		components: { MiddlewareSelector, FloatingButtons },
		props: {
			entrypoint: Object
		},
		data()
		{
			return {
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
			}
		}
	};
</script>

<style scoped>
	.entrypointEditor
	{
		background-color: #F0F0FF;
	}

	.flexRow input.ipAddressInput
	{
		width: 300px;
		max-width: 100%;
		box-sizing: border-box;
	}
</style>

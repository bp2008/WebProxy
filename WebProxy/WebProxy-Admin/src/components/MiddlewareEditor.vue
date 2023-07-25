<template>
	<div class="middlewareEditor primaryContainer">
		<FloatingButtons @delete="$emit('delete')" />
		<div class="flexRow">
			<label>Middleware Name</label>
			<input type="text" v-model="middleware.Id" />
		</div>
		<div class="flexRow">
			<label>Type</label>
			<select v-model="middleware.Type">
				<option v-for="middlewareType in availableMiddlewareTypes">{{middlewareType}}</option>
			</select>
		</div>
		<div v-if="middleware.Type === 'IPWhitelist'">
			<label>Whitelisted IP Ranges</label>
			<ArrayEditor v-model="middleware.WhitelistedIpRanges" arrayType="text" />
		</div>
		<div v-if="middleware.Type === 'HttpDigestAuth'">
			<label>Credentials</label>
			<ArrayEditor v-model="middleware.AuthCredentials" arrayType="credentials" />
		</div>
		<div v-if="middleware.Type === 'AddHttpHeaderToResponse'" class="flexRow">
			<label>Http Header</label>
			<input type="text" v-model="middleware.HttpHeader" />
		</div>
	</div>
</template>

<script>
	import store from '/src/library/store';
	import ArrayEditor from '/src/components/ArrayEditor.vue';
	import FloatingButtons from '/src/components/FloatingButtons.vue'

	export default {
		components: { ArrayEditor, FloatingButtons },
		props: {
			middleware: Object
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
			availableMiddlewareTypes()
			{
				return store.middlewareTypes;
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

<style scoped>
	.middlewareEditor
	{
		background-color: #F0FFF0;
	}
</style>

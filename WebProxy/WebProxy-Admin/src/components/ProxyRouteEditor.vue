<template>
	<div class="proxyRouteEditor primaryContainer">
		<FloatingButtons @delete="$emit('delete')" />
		<select v-model="proxyRoute.entrypointName">
			<option v-for="v in entrypointNames" :key="v">{{v}}</option>
		</select>
		-&gt;
		<select v-model="proxyRoute.exitpointName">
			<option v-for="v in exitpointNames" :key="v">{{v}}</option>
		</select>
	</div>
</template>

<script>
	import FloatingButtons from '/src/components/FloatingButtons.vue'
	import store from '/src/library/store';

	export default {
		components: { FloatingButtons },
		props: {
			proxyRoute: Object
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
			entrypointNames()
			{
				let arr = ["NONE SELECTED"];
				for (let i = 0; i < store.entrypoints.length; i++)
					arr.push(store.entrypoints[i].name);
				return arr;
			},
			exitpointNames()
			{
				let arr = ["NONE SELECTED"];
				for (let i = 0; i < store.exitpoints.length; i++)
					arr.push(store.exitpoints[i].name);
				return arr;
			}
		},
		watch:
		{
		}
	};
</script>

<style scoped>
	.proxyRouteEditor
	{
		background-color: #FFFFD0;
	}
</style>

<template>
	<div class="keyValueEditor">
		<input type="text" v-model="modelValue.Key" autocomplete="off" /> &#8594
		<input type="text" v-model="modelValue.Value" autocomplete="off" />
	</div>
</template>

<script>
	export default {
		props:
		{
			modelValue: {
				type: Object,
				required: true,
			},
		},
	};
</script>

<style scoped>
	.keyValueEditor
	{
		display: inline-block;
	}

		.keyValueEditor input
		{
			width: 210px;
			box-sizing: border-box;
		}

	@media (min-width: 750px)
	{
		.keyValueEditor input
		{
			width: 230px;
			box-sizing: border-box;
		}
	}

	@media (min-width: 800px)
	{
		.keyValueEditor input
		{
			width: 250px;
			box-sizing: border-box;
		}
	}
</style>

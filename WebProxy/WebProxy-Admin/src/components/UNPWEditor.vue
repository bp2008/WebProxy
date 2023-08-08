<template>
	<div class="unpwEditor">
		<input type="text" v-model="modelValue.User" autocomplete="off" /> &#8594
		<PasswordInput v-model="modelValue.Pass" :isPasswordInput="true"></PasswordInput>
	</div>
</template>

<script>
	import PasswordInput from '/src/components/PasswordInput.vue';

	export default {
		components: { PasswordInput },
		props:
		{
			modelValue: {
				type: Object,
				required: true,
			},
		},
		data()
		{
			return {
				passwordType: 'password',
			};
		},
		watch:
		{
		},
		methods:
		{
			togglePasswordVisibility()
			{
				this.passwordType = this.passwordType === 'password' ? 'text' : 'password';
			},
		},
	};
</script>

<style scoped>
	.unpwEditor
	{
		display: inline-block;
	}

		.unpwEditor input
		{
			width: 210px;
			box-sizing: border-box;
		}

	@media (min-width: 750px)
	{
		.unpwEditor input
		{
			width: 230px;
			box-sizing: border-box;
		}
	}

	@media (min-width: 800px)
	{
		.unpwEditor input
		{
			width: 250px;
			box-sizing: border-box;
		}
	}
</style>

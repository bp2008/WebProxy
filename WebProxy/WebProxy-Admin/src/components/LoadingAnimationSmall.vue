<template>
	<div class="loader">
		<div class="bar"></div>
		<div class="bar"></div>
		<div class="bar"></div>
	</div>
</template>

<script>
	export default {
	};
</script>

<style>
	.loader
	{
		display: flex;
		justify-content: space-between;
		width: 40px;
		height: 16px;
	}

	.bar
	{
		width: 4px;
		height: 16px;
		border-radius: 4px;
		animation: loading 1s ease-in-out infinite;
	}

		.bar:nth-child(1)
		{
			background-color: #3498db;
			animation-delay: 0;
		}

		.bar:nth-child(2)
		{
			background-color: #f3f3f3;
			animation-delay: 0.2s;
		}

		.bar:nth-child(3)
		{
			background-color: #3498db;
			animation-delay: 0.4s;
		}

	@keyframes loading
	{
		0%
		{
			transform: scale(1);
		}

		20%
		{
			transform: scale(1,2.5);
		}

		40%
		{
			transform: scale(1);
		}
	}

</style>

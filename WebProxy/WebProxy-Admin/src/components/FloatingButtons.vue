<template>
	<div class="floatingButtons">
		<span title="Drag me!"><ArrowIcon class="dragHandle" /></span>
		<span title="Delete this"><RemoveIcon @click="$emit('delete')" /></span>
	</div>
</template>

<script>
	import ArrowIcon from '/src/assets/arrows.svg?component'
	import RemoveIcon from '/src/assets/remove.svg?component'

	export default {
		components: { ArrowIcon, RemoveIcon },
	};
</script>

<style scoped>
	.floatingButtons
	{
		float: right;
		position: relative;
		top: -10px;
		right: -5px;
	}

		.floatingButtons > span > *
		{
			margin-left: 5px;
			box-sizing: border-box;
			min-width: 40px;
			min-height: 40px;
			width: 40px;
			height: 40px;
			cursor: pointer;
			user-select: none;
			margin-left: 5px;
			border: 1px solid #CCCCCC;
			border-radius: 7px;
		}

	.dragHandle
	{
		transform: rotate(90deg);
		box-shadow: 1px -1px 3px rgba(0,0,0,0.5);
	}
</style>

<template>
	<div class="expandCollapseButton"
		 tabindex="0"
		 @click="toggleExpansion"
		 @keypress.enter.space.prevent="toggleExpansion">
		<template v-if="allExpanded">
			<CollapseIcon />
			<span>Collapse {{title}}s</span>
		</template>
		<template v-else>
			<ExpandIcon />
			<span>Expand {{title}}s</span>
		</template>
	</div>
</template>

<script>
	import CollapseIcon from '/src/assets/unfold_less_double.svg?component';
	import ExpandIcon from '/src/assets/unfold_more_double.svg?component';
	import store from '/src/library/store';

	export default {
		components: { ExpandIcon, CollapseIcon },
		props:
		{
			title: {
				type: String,
				required: true
			}
		},
		data()
		{
			return {
				myCollectionSelector: null,
				myKeySelector: null,
			};
		},
		created()
		{
			if (this.title === "Entrypoint")
			{
				this.myCollectionSelector = () => store.entrypoints;
				this.myKeySelector = obj => obj.name;
			}
			else if (this.title === "Exitpoint")
			{
				this.myCollectionSelector = () => store.exitpoints;
				this.myKeySelector = obj => obj.name;
			}
			else if (this.title === "Middleware")
			{
				this.myCollectionSelector = () => store.middlewares;
				this.myKeySelector = obj => obj.Id;
			}
		},
		computed:
		{
			allExpanded()
			{
				let collection = this.myCollectionSelector();
				let keySelector = this.myKeySelector;
				for (let i = 0; i < collection.length; i++)
				{
					let key = this.title + "_" + keySelector(collection[i]);
					if (store.expansionState[key] === "0")
						return false;
				}
				return true;
			}
		},
		watch:
		{
		},
		methods:
		{
			toggleExpansion()
			{
				let expand = !this.allExpanded;
				let value = expand ? "1" : "0";
				let collection = this.myCollectionSelector();
				let keySelector = this.myKeySelector;
				for (let i = 0; i < collection.length; i++)
				{
					let key = this.title + "_" + keySelector(collection[i]);
					if (store.expansionState[key] !== value)
						store.expansionState[key] = value;
				}
			}
		}
	};
</script>

<style scoped>
	.expandCollapseButton
	{
		vertical-align: middle;
		display: inline-flex;
		align-items: center;
		padding: 4px;
		background-color: var(--button-background);
		border: 1px solid #666666;
		border-radius: 4px;
		box-shadow: var(--button-box-shadow);
		cursor: pointer;
		margin-left: 1em;
		font-size: 12pt;
		font-weight: bold;
	}

		.expandCollapseButton:hover
		{
			background-color: var(--button-active-background);
		}

		.expandCollapseButton span
		{
			padding: 0px 0.5em;
		}

	svg
	{
		width: 24px;
		height: 24px;
	}
</style>

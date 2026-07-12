<template>
	<div class="pluginSelector" v-if="allPluginRows.length">
		<label>Plugins: </label>
		<div class="pluginRow" v-for="m in allPluginRows">
			<label :class="{ orphan: m.orphan }"
				   :title="m.orphan ? 'This plugin instance no longer exists.' : ''"><input type="checkbox" v-model="m.checked" @change="onCheckChange" /> {{m.id}} <span v-if="m.orphan"> (missing)</span></label>
		</div>
	</div>
</template>

<script>
	import store from '/src/library/store';

	export default {
		components: {},
		props: {
			modelValue: {
				type: Array,
				required: true
			}
		},
		data()
		{
			return {
				allPluginRows: [],
				store
			};
		},
		created()
		{
			this.setupAllPluginRows();
		},
		computed:
		{
			allPluginInstances()
			{
				return store.pluginInstances;
			}
		},
		methods:
		{
			setupAllPluginRows()
			{
				this.allPluginRows = [];
				let added = {};
				for (let i = 0; i < this.allPluginInstances.length; i++)
				{
					let id = this.allPluginInstances[i].Id;
					let row = { id: id, checked: this.getIndex(id) > -1 };
					this.allPluginRows.push(row);
					added[id] = true;
				}
				for (let i = 0; i < this.modelValue.length; i++)
				{
					let id = this.modelValue[i];
					if (!added[id])
					{
						let row = { id: id, checked: true, orphan: true };
						this.allPluginRows.push(row);
						added[id] = true;
					}
				}
			},
			getIndex(id)
			{
				for (let i = 0; i < this.modelValue.length; i++)
					if (this.modelValue[i] === id)
						return i;
				return -1;
			},
			onCheckChange()
			{
				for (let i = 0; i < this.allPluginRows.length; i++)
				{
					let id = this.allPluginRows[i].id;
					let foundAt = this.getIndex(id);
					if (this.allPluginRows[i].checked)
					{
						if (foundAt === -1)
							this.modelValue.push(id);
					}
					else
					{
						if (foundAt !== -1)
							this.modelValue.splice(foundAt, 1);
					}
				}
			}
		},
		watch:
		{
			modelValue:
			{
				deep: true,
				handler()
				{
					this.setupAllPluginRows();
				}
			},
			allPluginInstances:
			{
				deep: true,
				handler()
				{
					this.setupAllPluginRows();
				}
			}
		}
	};
</script>

<style scoped>
	.pluginSelector
	{
		padding: 8px;
	}

	.pluginRow
	{
		margin-top: 0.3em;
	}

	.orphan
	{
		font-weight: bold;
		color: #880000;
	}
</style>

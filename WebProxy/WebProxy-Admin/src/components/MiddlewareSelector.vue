<template>
	<div class="middlewareSelector">
		<label>Middlewares: </label>
		<div class="middlewareRow" v-for="m in allMiddlewareRows">
			<label :class="{ orphan: m.orphan }"
				   :title="m.orphan ? 'This middleware no longer exists.' : ''"><input type="checkbox" v-model="m.checked" @change="onCheckChange" /> {{m.id}} <span v-if="m.orphan"> (missing)</span></label>
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
				allMiddlewareRows: []
			};
		},
		created()
		{
			this.setupAllMiddlewareRows();
		},
		computed:
		{
			allMiddlewares()
			{
				return store.middlewares;
			}
		},
		methods:
		{
			setupAllMiddlewareRows()
			{
				this.allMiddlewareRows = [];
				let added = {};
				for (let i = 0; i < this.allMiddlewares.length; i++)
				{
					let id = this.allMiddlewares[i].Id;
					let row = { id: id, checked: this.getIndex(id) > -1 };
					this.allMiddlewareRows.push(row);
					added[id] = true;
				}
				for (let i = 0; i < this.modelValue.length; i++)
				{
					let id = this.modelValue[i];
					if (!added[id])
					{
						let row = { id: id, checked: true, orphan: true };
						this.allMiddlewareRows.push(row);
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
				for (let i = 0; i < this.allMiddlewareRows.length; i++)
				{
					let id = this.allMiddlewareRows[i].id;
					let foundAt = this.getIndex(id);
					if (this.allMiddlewareRows[i].checked)
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
					this.setupAllMiddlewareRows();
				}
			},
			allMiddlewares:
			{
				deep: true,
				handler()
				{
					this.setupAllMiddlewareRows();
				}
			}
		}
	};
</script>

<style scoped>
	.middlewareSelector
	{
		border: 1px dashed #000000;
		border-radius: 5px;
		padding: 8px;
	}

	.middlewareRow
	{
		margin-top: 0.3em;
	}

	.orphan
	{
		font-weight: bold;
		color: #880000;
	}
</style>

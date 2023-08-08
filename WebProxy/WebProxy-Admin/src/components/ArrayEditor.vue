<template>
	<div class="arrayEditor">
		<div class="arrayElement" v-for="(item, index) in modelValue" :key="index">
			<select v-if="allowedValues" v-model="modelValue[index]" autocomplete="off">
				<option v-for="v in allowedValues" :key="v">{{v}}</option>
			</select>
			<UNPWEditor v-else-if="arrayType === 'credentials'" v-model="modelValue[index]" class="unpwEditor" />
			<input v-else :type="arrayType" v-model="modelValue[index]" autocomplete="off" />
			<input class="removeButton" type="button" value="-" @click="removeArrayItem(index)" />
		</div>
		<div>
			<input class="addButton" type="button" value="+" @click="addArrayItem" />
		</div>
	</div>
</template>

<script>
	import { reactive } from 'vue';
	import UNPWEditor from '/src/components/UNPWEditor.vue';

	export default {
		components: { UNPWEditor },
		props: {
			modelValue: {
				type: Array,
				required: true
			},
			allowedValues: {
				type: Object,
				default: null // If [allowedValues] is not null, the user is presented with a dropdown list containing these options.
			},
			arrayType: {
				type: String,
				default: "text" // If [allowedValues] is null, [arrayType] is the html input type to use. A special string 'credentials' will yield user name and password inputs for each array item.
			}
		},
		methods:
		{
			addArrayItem()
			{
				let d;
				if (this.allowedValues && this.allowedValues.length > 0)
					d = this.allowedValues[0];
				else if (this.arrayType === "credentials")
					d = {};
				else if (this.arrayType === "number")
					d = 0;
				else
					d = "";
				this.modelValue.push(d);
			},
			removeArrayItem(index)
			{
				this.modelValue.splice(index, 1);
			}
		}
	};
</script>

<style scoped>
	.removeButton
	{
		margin-left: 5px;
	}

	.arrayElement
	{
		margin-bottom: 5px;
		display: flex;
		/*align-items: flex-start;*/
	}

		.arrayElement > *:first-child
		{
			flex: 1 1 auto;
		}

		.arrayElement > *:last-child
		{
			flex: 0 0 auto;
		}

		.arrayElement > .unpwEditor:first-child
		{
			flex: 0 1 auto;
		}
</style>

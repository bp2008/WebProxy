<template>
	<PrimaryContainer class="pluginInstanceEditor" title="Plugin Instance" :name="pluginInstance.Id">
		<div class="flexRow">
			<label>Name</label>
			<input type="text" v-model="pluginInstance.Id" autocomplete="off" />
			<div class="comment" v-if="store.showHelp">You can change the Plugin Instance Name after creation, but you must manually update all Entrypoints and Exitpoints that used it.</div>
		</div>
		<div class="flexRow">
			<label>Plugin Type</label>
			<select v-model="pluginInstance.PluginTypeName">
				<option v-for="t in allPluginTypes" :key="t.TypeFullName" :value="t.TypeFullName">{{t.Name}} — {{t.PackageName}}</option>
				<option v-if="isOrphan && pluginInstance.PluginTypeName" :value="pluginInstance.PluginTypeName">{{pluginInstance.PluginTypeName}} (missing)</option>
			</select>
			<div class="comment" v-if="store.showHelp">The plugin (from the Installed Plugins list) which this instance uses.  Each instance has its own copy of the plugin's options, so you can attach the same plugin to different Entrypoints/Exitpoints with different options by creating multiple instances.</div>
		</div>
		<div class="errorText" v-if="isOrphan && pluginInstance.PluginTypeName">
			The plugin type "{{pluginInstance.PluginTypeName}}" is not installed.  This plugin instance is faulted, and requests routed through it will receive an error response until the plugin is installed or this instance is detached/deleted.
		</div>
		<div class="errorText" v-if="instanceError">
			{{instanceError}}
		</div>
		<div class="pluginDescription" v-if="selectedType && selectedType.Description">
			{{selectedType.Description}}
		</div>
		<template v-if="selectedType">
			<div class="pluginVersion" v-if="store.showHelp">Version {{selectedType.Version}} from package "{{selectedType.PackageName}}"</div>
			<template v-if="selectedType.OptionFields && selectedType.OptionFields.length">
				<div class="flexRow optionRow" v-for="field in selectedType.OptionFields" :key="field.Key">
					<template v-if="field.FieldType === 'bool'">
						<label><input type="checkbox" v-model="pluginInstance.Options[field.Key]" /> {{field.DisplayName}}</label>
					</template>
					<template v-else-if="field.FieldType === 'string'">
						<label>{{field.DisplayName}}</label>
						<input type="text" v-model="pluginInstance.Options[field.Key]" :placeholder="field.Placeholder ? field.Placeholder : ''" autocomplete="off" />
					</template>
					<template v-else-if="field.FieldType === 'multiline'">
						<label>{{field.DisplayName}}</label>
						<textarea v-model="pluginInstance.Options[field.Key]" :placeholder="field.Placeholder ? field.Placeholder : ''" autocomplete="off"></textarea>
					</template>
					<template v-else-if="field.FieldType === 'number'">
						<label>{{field.DisplayName}}</label>
						<input type="number" v-model.number="pluginInstance.Options[field.Key]"
							   :min="typeof field.Min === 'number' ? field.Min : undefined"
							   :max="typeof field.Max === 'number' ? field.Max : undefined"
							   :step="field.IsInteger ? 1 : 'any'"
							   autocomplete="off" />
					</template>
					<template v-else-if="field.FieldType === 'enum'">
						<label>{{field.DisplayName}}</label>
						<select v-model="pluginInstance.Options[field.Key]">
							<option v-for="v in field.EnumValues" :key="v">{{v}}</option>
						</select>
					</template>
					<template v-else-if="field.FieldType === 'stringArray'">
						<label>{{field.DisplayName}}</label>
						<ArrayEditor v-if="Array.isArray(pluginInstance.Options[field.Key])" v-model="pluginInstance.Options[field.Key]" arrayType="text" />
					</template>
					<div class="comment" v-if="store.showHelp && field.HelpText">{{field.HelpText}}</div>
				</div>
			</template>
			<div v-else>
				This plugin has no options.
			</div>
		</template>
	</PrimaryContainer>
</template>
<script>
	import store from '/src/library/store';
	import ArrayEditor from '/src/components/ArrayEditor.vue';
	import PrimaryContainer from '/src/components/PrimaryContainer.vue';

	export default {
		components: { ArrayEditor, PrimaryContainer },
		props: {
			pluginInstance: Object
		},
		data()
		{
			return {
				store
			};
		},
		created()
		{
			this.ensureOptionDefaults();
		},
		computed:
		{
			allPluginTypes()
			{
				let arr = [];
				if (store.installedPlugins)
				{
					for (let i = 0; i < store.installedPlugins.length; i++)
					{
						let pkg = store.installedPlugins[i];
						if (pkg.PluginTypes)
							for (let n = 0; n < pkg.PluginTypes.length; n++)
								arr.push(pkg.PluginTypes[n]);
					}
				}
				return arr;
			},
			selectedType()
			{
				for (let i = 0; i < this.allPluginTypes.length; i++)
					if (this.allPluginTypes[i].TypeFullName === this.pluginInstance.PluginTypeName)
						return this.allPluginTypes[i];
				return null;
			},
			isOrphan()
			{
				return !this.selectedType;
			},
			instanceError()
			{
				if (store.pluginInstanceErrors)
					return store.pluginInstanceErrors[this.pluginInstance.Id];
				return null;
			}
		},
		methods:
		{
			ensureOptionDefaults()
			{
				if (!this.pluginInstance.Options || typeof this.pluginInstance.Options !== "object")
					this.pluginInstance.Options = {};
				let t = this.selectedType;
				if (!t || !t.OptionFields)
					return;
				for (let i = 0; i < t.OptionFields.length; i++)
				{
					let field = t.OptionFields[i];
					let currentValue = this.pluginInstance.Options[field.Key];
					if (field.FieldType === "stringArray")
					{
						if (!Array.isArray(currentValue))
							this.pluginInstance.Options[field.Key] = Array.isArray(field.DefaultValue) ? field.DefaultValue.slice() : [];
					}
					else if (typeof currentValue === "undefined" || currentValue === null)
						this.pluginInstance.Options[field.Key] = typeof field.DefaultValue === "undefined" ? null : field.DefaultValue;
				}
			}
		},
		watch:
		{
			"pluginInstance.PluginTypeName"()
			{
				this.ensureOptionDefaults();
			}
		}
	};
</script>
<style scoped>
	.errorText
	{
		font-weight: bold;
		color: #FF3300;
		white-space: pre-wrap;
	}

	.pluginDescription
	{
		white-space: pre-wrap;
	}

	.pluginVersion
	{
		opacity: 0.75;
		font-size: 0.85em;
	}

	.optionRow textarea
	{
		width: 550px;
		max-width: 100%;
		min-height: 75px;
		box-sizing: border-box;
	}

	.optionRow input[type="text"]
	{
		width: 550px;
		max-width: 100%;
		box-sizing: border-box;
	}
</style>

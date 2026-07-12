using System;
using System.Collections.Generic;
using WebProxy.Plugins;

namespace WebProxy
{
	/// <summary>
	/// Describes one plugin type (a class deriving from <see cref="WebProxyPlugin"/>) which was discovered in an installed plugin package.
	/// </summary>
	public class LoadedPluginType
	{
		/// <summary>
		/// Name of the plugin package (the subdirectory of the "Plugins" directory which the plugin was loaded from).
		/// </summary>
		public string PackageName;
		/// <summary>
		/// Full name of the plugin type (namespace and class name).  This is the identifier which configured plugin instances refer to.
		/// </summary>
		public string TypeFullName;
		/// <summary>
		/// Short display name of the plugin type (class name).
		/// </summary>
		public string Name;
		/// <summary>
		/// Version string of the assembly which the plugin type was loaded from.
		/// </summary>
		public string Version;
		/// <summary>
		/// Description provided by the plugin, or null.
		/// </summary>
		public string Description;
		/// <summary>
		/// Option field schema built from the plugin's options class.  Empty if the plugin has no options.
		/// </summary>
		public List<PluginOptionField> OptionFields;
		/// <summary>
		/// The plugin Type, used to construct instances.  Null if the plugin failed to load.
		/// </summary>
		[Newtonsoft.Json.JsonIgnore]
		public Type Type;
	}
	/// <summary>
	/// Describes an installed plugin package and the plugin types (or load error) within it.  Serialized to the Admin Console.
	/// </summary>
	public class LoadedPluginPackage
	{
		/// <summary>
		/// Name of the plugin package (subdirectory name within the "Plugins" directory).
		/// </summary>
		public string PackageName;
		/// <summary>
		/// Names of the DLL files in the package directory.
		/// </summary>
		public string[] Files;
		/// <summary>
		/// Plugin types discovered in this package.
		/// </summary>
		public List<LoadedPluginType> PluginTypes = new List<LoadedPluginType>();
		/// <summary>
		/// If not null, describes an error which occurred while loading this package.
		/// </summary>
		public string LoadError;
	}
}

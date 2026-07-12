using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebProxy
{
	/// <summary>
	/// <para>A configured plugin instance.  Like a Middleware, a plugin instance is defined once and can then be attached to any number of Entrypoints and Exitpoints via its <see cref="Id"/>.</para>
	/// <para>Each plugin instance has its own options, so the same plugin can be attached to different Entrypoints/Exitpoints with different configurations by defining multiple plugin instances.</para>
	/// </summary>
	public class PluginInstance
	{
		/// <summary>
		/// User-defined unique identifier for this plugin instance.
		/// </summary>
		public string Id = "";
		/// <summary>
		/// Full name of the plugin type (namespace and class name) which this instance uses, as reported by the plugin manager (e.g. "MyPlugins.CustomRequestLogger").
		/// </summary>
		public string PluginTypeName = "";
		/// <summary>
		/// Raw option values for this instance, keyed by the option field name declared in the plugin's options class.  May be null if the plugin has no options.
		/// </summary>
		public JObject Options;
		/// <summary>
		/// Returns a string describing the plugin instance.
		/// </summary>
		/// <returns>A string describing the plugin instance.</returns>
		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}

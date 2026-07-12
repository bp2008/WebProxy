using System;
using System.Threading.Tasks;

namespace WebProxy.Plugins
{
	/// <summary>
	/// <para>Base class for all WebProxy plugins.  To create a plugin, create a class library targeting a compatible .NET version (e.g. net10.0, without a platform suffix so the plugin remains cross-platform), reference WebProxy.Plugins.dll and BPUtil.dll from a WebProxy installation, and create one or more public non-abstract classes deriving from <see cref="WebProxyPlugin"/> or <see cref="WebProxyPlugin{TOptions}"/>.</para>
	/// <para>Deploy the plugin by uploading the compiled DLL via the "Plugins" page of the WebProxy Admin Console (or by placing it in a subdirectory of the "Plugins" directory within WebProxy's data folder).</para>
	/// <para>Lifecycle: One instance of the plugin class is constructed for each configured plugin instance ("attachment") that uses it.  The constructor must be trivial and must not throw; perform expensive initialization in <see cref="OnLoadedAsync"/>.  Instances are discarded and recreated whenever WebProxy settings are saved or plugin files change.</para>
	/// <para>Thread-safety: <see cref="OnRequestAsync"/> may be called concurrently on the same instance by many requests.  Any mutable state shared between requests must be synchronized by the plugin.</para>
	/// </summary>
	public abstract class WebProxyPlugin
	{
		/// <summary>
		/// Provides host services (logging, data directories) to the plugin.  Assigned by the host before <see cref="OnLoadedAsync"/> is called.
		/// </summary>
		public IPluginHost Host { get; private set; }
		/// <summary>
		/// The user-defined unique Id of the configured plugin instance which this object was created for.  Assigned by the host before <see cref="OnLoadedAsync"/> is called.
		/// </summary>
		public string InstanceId { get; private set; }
		/// <summary>
		/// The deserialized options object (of type <see cref="OptionsType"/>) for this plugin instance, or null if <see cref="OptionsType"/> is null.  Assigned by the host before <see cref="OnLoadedAsync"/> is called.
		/// </summary>
		public object OptionsObject { get; private set; }
		/// <summary>
		/// Optionally overridden by the plugin to provide a description of the plugin which is shown in the WebProxy Admin Console.
		/// </summary>
		public virtual string Description => null;
		/// <summary>
		/// <para>Optionally overridden by the plugin to declare the type of the plugin's options class, or null if the plugin has no options.</para>
		/// <para>The options class must have a public parameterless constructor which assigns default values.  Public fields and public read/write properties of supported types (string, bool, integer and floating-point numbers, enums, string arrays/lists) are exposed as editable options in the WebProxy Admin Console, individually for each configured plugin instance.  Use <see cref="PluginOptionAttribute"/> to provide display names and help text.</para>
		/// <para>Consider deriving from <see cref="WebProxyPlugin{TOptions}"/> instead of overriding this directly.</para>
		/// </summary>
		public virtual Type OptionsType => null;
		/// <summary>
		/// Called by the WebProxy host to initialize this plugin instance.  Plugins must not call this method.
		/// </summary>
		/// <param name="host">Host services object.</param>
		/// <param name="instanceId">Id of the configured plugin instance.</param>
		/// <param name="optionsObject">Deserialized options object, or null.</param>
		/// <exception cref="InvalidOperationException">If this plugin object was already initialized.</exception>
		internal void InitializePlugin(IPluginHost host, string instanceId, object optionsObject)
		{
			if (Host != null)
				throw new InvalidOperationException("This plugin object was already initialized.");
			Host = host;
			InstanceId = instanceId;
			OptionsObject = optionsObject;
		}
		/// <summary>
		/// <para>Called once after the plugin instance has been constructed and its options have been assigned.  Perform expensive initialization here (e.g. opening files, starting timers).</para>
		/// <para>If this method throws, the plugin instance is considered faulted and will not receive requests; the error is shown in the WebProxy Admin Console.</para>
		/// </summary>
		public virtual Task OnLoadedAsync() { return Task.CompletedTask; }
		/// <summary>
		/// <para>Called once when the plugin instance is being discarded (settings were saved, plugin files changed, or the service is shutting down).  Release resources here.</para>
		/// <para>After this method is called, the instance will not receive further requests.</para>
		/// </summary>
		public virtual Task OnUnloadingAsync() { return Task.CompletedTask; }
		/// <summary>
		/// <para>Called for each incoming request that matched an Entrypoint or Exitpoint which this plugin instance is attached to.  This is called after WebProxy's built-in middlewares (IP whitelisting, authentication, etc) have accepted the request, and before the request is proxied or otherwise handled.</para>
		/// <para>The plugin may:</para>
		/// <para>* Inspect and modify the request (HTTP method, headers) via <see cref="PluginRequestContext.Request"/>.</para>
		/// <para>* Change where the request will be proxied to via <see cref="PluginRequestContext.DestinationUri"/> (only for Exitpoints of type WebProxy).</para>
		/// <para>* Write its own response (fully-buffered or streaming) via <see cref="PluginRequestContext.Response"/>, then return <see cref="PluginRequestAction.Handled"/>.</para>
		/// <para>* Return <see cref="PluginRequestAction.CloseConnection"/> to close the client's connection without a response (e.g. banning behavior).</para>
		/// <para>* Register a response hook via <see cref="PluginRequestContext.AddResponseHeadersHook"/> to inspect/modify proxied response headers and optionally the response body.</para>
		/// <para>If this method throws an exception, WebProxy responds with "500 Internal Server Error" and does not proxy the request (fail-closed).</para>
		/// </summary>
		/// <param name="context">Context object providing access to the request and to plugin capabilities.</param>
		/// <returns>A <see cref="PluginRequestAction"/> instructing WebProxy how to proceed.</returns>
		public abstract Task<PluginRequestAction> OnRequestAsync(PluginRequestContext context);
	}
	/// <summary>
	/// Base class for WebProxy plugins which have an options class.  The options class must have a public parameterless constructor which assigns default values.
	/// </summary>
	/// <typeparam name="TOptions">Type of the plugin's options class.</typeparam>
	public abstract class WebProxyPlugin<TOptions> : WebProxyPlugin where TOptions : class, new()
	{
		private TOptions _fallbackOptions;
		/// <inheritdoc/>
		public sealed override Type OptionsType => typeof(TOptions);
		/// <summary>
		/// Gets the options for this plugin instance.  Never null; if no options were assigned by the host, a default-constructed instance is returned.
		/// </summary>
		public TOptions Options
		{
			get
			{
				TOptions o = OptionsObject as TOptions;
				if (o != null)
					return o;
				if (_fallbackOptions == null)
					_fallbackOptions = new TOptions();
				return _fallbackOptions;
			}
		}
	}
}

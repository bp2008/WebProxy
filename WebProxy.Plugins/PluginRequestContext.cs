using BPUtil.SimpleHttp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy.Plugins
{
	/// <summary>
	/// Provides a plugin with access to an incoming request and to plugin capabilities.  One instance is created per request and is shared by all plugins that see the request.
	/// </summary>
	public class PluginRequestContext
	{
		/// <summary>
		/// The HttpProcessor handling the client's connection.  Provides full access to the request and response objects and connection metadata (client IP address, TLS state, etc).
		/// </summary>
		public HttpProcessor Processor { get; }
		/// <summary>
		/// <para>The client's request.  Plugins may modify the HTTP method and request headers before the request is proxied.</para>
		/// <para>Note: When a request is proxied, hop-by-hop headers ("Connection", "Upgrade", "Host", etc) are computed by the proxy utility and cannot be overridden via this header collection.  The "Accept-Encoding" request header CAN be overridden (e.g. set it to "identity" if your plugin intends to manipulate the response body and does not want to deal with compression).</para>
		/// </summary>
		public SimpleHttpRequest Request => Processor.Request;
		/// <summary>
		/// <para>The response object for the client's connection.  If the plugin writes or configures a response here, it must return <see cref="PluginRequestAction.Handled"/> from <see cref="WebProxyPlugin.OnRequestAsync"/> so that WebProxy does not attempt to proxy the request.</para>
		/// <para>To respond all at once: <c>context.Response.FullResponseUTF8(body, "text/html; charset=utf-8")</c> and similar methods.</para>
		/// <para>To respond in a streaming fashion: configure the response (e.g. <c>context.Response.Set(...)</c>), then write to the stream returned by <c>context.Response.GetResponseStreamAsync()</c>.</para>
		/// </summary>
		public SimpleHttpResponse Response => Processor.Response;
		/// <summary>
		/// Name of the Entrypoint which matched this request.
		/// </summary>
		public string EntrypointName { get; }
		/// <summary>
		/// Name of the Exitpoint which matched this request.
		/// </summary>
		public string ExitpointName { get; }
		/// <summary>
		/// True if the matched Exitpoint is of type WebProxy, meaning the request will be proxied to <see cref="DestinationUri"/> if allowed to continue.
		/// </summary>
		public bool RequestWillBeProxied { get; }
		/// <summary>
		/// <para>If <see cref="RequestWillBeProxied"/>, this is the URI which the request will be proxied to (scheme, host, port, path and query).  The plugin may assign a new absolute URI to change where the request is proxied to, including changing the path or query string.</para>
		/// <para>Null if the matched Exitpoint is not of type WebProxy; assigning a value in that case has no effect.</para>
		/// </summary>
		public Uri DestinationUri { get; set; }
		/// <summary>
		/// <para>If <see cref="RequestWillBeProxied"/>, this is the host string used for the outgoing "Host" header and TLS Server Name Indication, or null to derive it from <see cref="DestinationUri"/>.  The plugin may assign a different value.</para>
		/// </summary>
		public string DestinationHostHeader { get; set; }
		/// <summary>
		/// A CancellationToken which is cancelled if the request is aborted (e.g. server shutdown).
		/// </summary>
		public CancellationToken CancellationToken { get; }

		private List<KeyValuePair<string, Func<PluginResponseContext, Task>>> responseHeadersHooks;
		/// <summary>
		/// Id of the plugin instance whose OnRequestAsync is currently executing.  Maintained by the WebProxy host so that response hooks can be attributed to the plugin that registered them.
		/// </summary>
		internal string CurrentPluginInstanceId;

		internal PluginRequestContext(HttpProcessor processor, string entrypointName, string exitpointName, bool requestWillBeProxied, Uri destinationUri, string destinationHostHeader, CancellationToken cancellationToken)
		{
			Processor = processor;
			EntrypointName = entrypointName;
			ExitpointName = exitpointName;
			RequestWillBeProxied = requestWillBeProxied;
			DestinationUri = destinationUri;
			DestinationHostHeader = destinationHostHeader;
			CancellationToken = cancellationToken;
		}

		/// <summary>
		/// <para>Registers a callback which will be awaited after response headers have been received from the remote server, and before they are sent to the client.  The callback may inspect and modify the response status and headers, and may additionally register a response body filter via <see cref="PluginResponseContext.SetResponseBodyFilter"/> or <see cref="PluginResponseContext.SetBufferedBodyTransform"/>.</para>
		/// <para>Response hooks only run for requests which are proxied by WebProxy (Exitpoint type WebProxy, <see cref="RequestWillBeProxied"/> == true), and only when the request completes normally enough to produce a proxied response (e.g. not on gateway timeout).</para>
		/// <para>Hooks run in the order they were registered.  If a hook throws an exception, the proxied request is aborted.</para>
		/// </summary>
		/// <param name="hook">Asynchronous callback receiving a <see cref="PluginResponseContext"/>.</param>
		/// <exception cref="ArgumentNullException">If the hook is null.</exception>
		public void AddResponseHeadersHook(Func<PluginResponseContext, Task> hook)
		{
			if (hook == null)
				throw new ArgumentNullException(nameof(hook));
			if (responseHeadersHooks == null)
				responseHeadersHooks = new List<KeyValuePair<string, Func<PluginResponseContext, Task>>>();
			responseHeadersHooks.Add(new KeyValuePair<string, Func<PluginResponseContext, Task>>(CurrentPluginInstanceId, hook));
		}
		/// <summary>
		/// Gets the list of registered response headers hooks (each paired with the Id of the plugin instance that registered it), or null if none were registered.  Used by the WebProxy host.
		/// </summary>
		internal List<KeyValuePair<string, Func<PluginResponseContext, Task>>> ResponseHeadersHooks => responseHeadersHooks;
	}
}

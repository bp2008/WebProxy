namespace WebProxy.Plugins
{
	/// <summary>
	/// Returned by <see cref="WebProxyPlugin.OnRequestAsync"/> to instruct WebProxy how to proceed with the request.
	/// </summary>
	public enum PluginRequestAction
	{
		/// <summary>
		/// Continue normal request processing (remaining plugins run, then the request is proxied or otherwise handled by WebProxy).
		/// </summary>
		Continue = 0,
		/// <summary>
		/// <para>The plugin has handled the request; WebProxy will not proxy it and no further plugins will see it.</para>
		/// <para>Before returning this, the plugin should have either written a response (e.g. via <c>context.Response.FullResponseUTF8(...)</c> or by streaming to the stream returned from <c>context.Response.GetResponseStreamAsync()</c>) or configured the Response object so WebProxy's web server can flush it automatically.</para>
		/// </summary>
		Handled = 1,
		/// <summary>
		/// The client's connection will be closed without sending any response (useful for banning/blocking behavior).  No further plugins will see the request and it will not be proxied.
		/// </summary>
		CloseConnection = 2
	}
}

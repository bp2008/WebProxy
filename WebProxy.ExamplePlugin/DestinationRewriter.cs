using System;
using System.Threading.Tasks;
using WebProxy.Plugins;

namespace WebProxy.ExamplePlugin
{
	/// <summary>
	/// Options for <see cref="DestinationRewriter"/>.
	/// </summary>
	public class DestinationRewriterOptions
	{
		[PluginOption(DisplayName = "If Path Starts With",
			HelpText = "Requests whose path starts with this case-insensitive string are rewritten to go to the alternate destination origin.  Example: \"/api/\".  If empty, all requests are rewritten.")]
		public string IfPathStartsWith = "";

		[PluginOption(DisplayName = "Alternate Destination Origin",
			HelpText = "Matching requests are proxied to this origin instead of the Exitpoint's Destination Origin.  Example: \"http://127.0.0.1:8081\".  If empty, the plugin does nothing.",
			Placeholder = "http://127.0.0.1:8081")]
		public string AlternateDestinationOrigin = "";

		[PluginOption(DisplayName = "Alternate Destination Host Header",
			HelpText = "Optional host string to use for the outgoing \"Host\" header and TLS Server Name Indication of rewritten requests.  If empty, the host is derived from the Alternate Destination Origin.")]
		public string AlternateDestinationHostHeader = "";
	}
	/// <summary>
	/// Example plugin which reroutes matching requests to a different destination server.  Demonstrates modification of the destination URI which a request will be proxied to.
	/// </summary>
	public class DestinationRewriter : WebProxyPlugin<DestinationRewriterOptions>
	{
		/// <inheritdoc/>
		public override string Description => "Proxies matching requests to an alternate destination origin instead of the Exitpoint's normal Destination Origin.";
		/// <inheritdoc/>
		public override Task<PluginRequestAction> OnRequestAsync(PluginRequestContext context)
		{
			if (context.RequestWillBeProxied
				&& !string.IsNullOrWhiteSpace(Options.AlternateDestinationOrigin)
				&& Uri.TryCreate(Options.AlternateDestinationOrigin, UriKind.Absolute, out Uri alternateOrigin))
			{
				string path = context.DestinationUri.PathAndQuery;
				if (string.IsNullOrEmpty(Options.IfPathStartsWith) || path.StartsWith(Options.IfPathStartsWith, StringComparison.OrdinalIgnoreCase))
				{
					UriBuilder builder = new UriBuilder(context.DestinationUri);
					builder.Scheme = alternateOrigin.Scheme;
					builder.Host = alternateOrigin.DnsSafeHost;
					builder.Port = alternateOrigin.Port;
					context.DestinationUri = builder.Uri;
					if (!string.IsNullOrWhiteSpace(Options.AlternateDestinationHostHeader))
						context.DestinationHostHeader = Options.AlternateDestinationHostHeader;
					else
						context.DestinationHostHeader = null;
				}
			}
			return Task.FromResult(PluginRequestAction.Continue);
		}
	}
}

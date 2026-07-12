using System;
using System.Text;
using System.Threading.Tasks;
using WebProxy.Plugins;

namespace WebProxy.ExamplePlugin
{
	/// <summary>
	/// Options for <see cref="TextReplaceInResponse"/>.
	/// </summary>
	public class TextReplaceInResponseOptions
	{
		[PluginOption(DisplayName = "Find",
			HelpText = "Case-sensitive text to find in response bodies.  If empty, the plugin does nothing.")]
		public string Find = "";

		[PluginOption(DisplayName = "Replace With")]
		public string ReplaceWith = "";

		[PluginOption(DisplayName = "Content-Type Substring",
			HelpText = "Only responses whose Content-Type header contains this substring (case-insensitive) are modified.")]
		public string ContentTypeSubstring = "text/html";
	}
	/// <summary>
	/// <para>Example plugin which replaces text in proxied response bodies.  Demonstrates response header inspection and response body manipulation.</para>
	/// <para>The body transform used here buffers the entire response body (decoding gzip/deflate/br compression automatically) and corrects the Content-Length and Content-Encoding headers.  The response body is assumed to be UTF-8 text, which is sufficient for an example.</para>
	/// </summary>
	public class TextReplaceInResponse : WebProxyPlugin<TextReplaceInResponseOptions>
	{
		/// <inheritdoc/>
		public override string Description => "Replaces text in proxied response bodies (UTF-8 responses only), for responses matching a configurable Content-Type.";
		/// <inheritdoc/>
		public override Task<PluginRequestAction> OnRequestAsync(PluginRequestContext context)
		{
			if (!string.IsNullOrEmpty(Options.Find) && context.RequestWillBeProxied)
			{
				context.AddResponseHeadersHook((PluginResponseContext responseContext) =>
				{
					string contentType = responseContext.Response.Headers.Get("Content-Type");
					if (contentType != null && contentType.IndexOf(Options.ContentTypeSubstring ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						responseContext.SetBufferedBodyTransform((byte[] body) =>
						{
							string text = Encoding.UTF8.GetString(body);
							text = text.Replace(Options.Find, Options.ReplaceWith ?? "");
							return Task.FromResult(Encoding.UTF8.GetBytes(text));
						});
					}
					return Task.CompletedTask;
				});
			}
			return Task.FromResult(PluginRequestAction.Continue);
		}
	}
}

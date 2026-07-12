using System;
using System.Threading.Tasks;
using WebProxy.Plugins;

namespace WebProxy.ExamplePlugin
{
	/// <summary>
	/// Options for <see cref="UserAgentBlocker"/>.
	/// </summary>
	public class UserAgentBlockerOptions
	{
		[PluginOption(DisplayName = "Blocked User-Agent Substrings",
			HelpText = "Requests with a User-Agent header containing any of these substrings (case-insensitive) are blocked.")]
		public string[] BlockedUserAgentSubstrings = new string[0];

		[PluginOption(DisplayName = "Close Connection Instead Of Responding",
			HelpText = "If enabled, blocked clients have their connection closed without any response.  Otherwise, blocked requests receive a \"403 Forbidden\" response.")]
		public bool CloseConnectionInsteadOfResponding = false;
	}
	/// <summary>
	/// Example plugin which blocks requests based on the User-Agent header.  Demonstrates responding to a request from a plugin and closing connections without a response (banning behavior).
	/// </summary>
	public class UserAgentBlocker : WebProxyPlugin<UserAgentBlockerOptions>
	{
		/// <inheritdoc/>
		public override string Description => "Blocks requests whose User-Agent header contains any of the configured substrings, either by responding with \"403 Forbidden\" or by closing the connection without a response.";
		/// <inheritdoc/>
		public override Task<PluginRequestAction> OnRequestAsync(PluginRequestContext context)
		{
			if (Options.BlockedUserAgentSubstrings != null && Options.BlockedUserAgentSubstrings.Length > 0)
			{
				string userAgent = context.Request.Headers.Get("User-Agent") ?? "";
				foreach (string blocked in Options.BlockedUserAgentSubstrings)
				{
					if (string.IsNullOrWhiteSpace(blocked))
						continue;
					if (userAgent.IndexOf(blocked, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						if (Options.CloseConnectionInsteadOfResponding)
							return Task.FromResult(PluginRequestAction.CloseConnection);
						context.Response.Simple("403 Forbidden", "This client is not allowed to access this resource.");
						return Task.FromResult(PluginRequestAction.Handled);
					}
				}
			}
			return Task.FromResult(PluginRequestAction.Continue);
		}
	}
}

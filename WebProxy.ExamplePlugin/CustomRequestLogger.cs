using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BPUtil.SimpleHttp;
using WebProxy.Plugins;

namespace WebProxy.ExamplePlugin
{
	/// <summary>
	/// Log verbosity options for <see cref="CustomRequestLogger"/>.
	/// </summary>
	public enum LogVerbosity
	{
		/// <summary>
		/// One line per request: timestamp, client IP, method, URL.
		/// </summary>
		Basic,
		/// <summary>
		/// Like Basic, but all request headers are also logged.
		/// </summary>
		Headers
	}
	/// <summary>
	/// Options for <see cref="CustomRequestLogger"/>.  Each plugin instance gets its own copy of these options.
	/// </summary>
	public class CustomRequestLoggerOptions
	{
		[PluginOption(DisplayName = "Log File Path",
			HelpText = "Full path of the log file to append to.  If empty, \"requests.log\" in the plugin instance's data directory is used.",
			Placeholder = "(plugin data directory)/requests.log")]
		public string LogFilePath = "";

		[PluginOption(DisplayName = "Verbosity",
			HelpText = "Basic: one line per request.  Headers: also log all request headers.")]
		public LogVerbosity Verbosity = LogVerbosity.Basic;

		[PluginOption(DisplayName = "Log Response Status",
			HelpText = "If enabled, the response status of proxied requests is also logged (via a response hook).")]
		public bool LogResponseStatus = true;
	}
	/// <summary>
	/// Example plugin which logs web requests to a text file in a custom format.  Demonstrates plugin options, the per-request hook, and response hooks.
	/// </summary>
	public class CustomRequestLogger : WebProxyPlugin<CustomRequestLoggerOptions>
	{
		private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
		private string logFilePath;
		/// <inheritdoc/>
		public override string Description => "Logs each web request to a text file in a custom format.  The log file path and verbosity are configurable per plugin instance.";
		/// <inheritdoc/>
		public override Task OnLoadedAsync()
		{
			if (string.IsNullOrWhiteSpace(Options.LogFilePath))
				logFilePath = Path.Combine(Host.DataDirectoryPath, "requests.log");
			else
			{
				logFilePath = Options.LogFilePath;
				string dir = Path.GetDirectoryName(Path.GetFullPath(logFilePath));
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);
			}
			Host.Log("CustomRequestLogger will log to: " + logFilePath);
			return Task.CompletedTask;
		}
		/// <inheritdoc/>
		public override async Task<PluginRequestAction> OnRequestAsync(PluginRequestContext context)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
			sb.Append('\t').Append(context.Processor.RemoteIPAddressStr);
			sb.Append('\t').Append(context.Request.HttpMethod);
			sb.Append('\t').Append(context.Request.Url);
			if (Options.Verbosity >= LogVerbosity.Headers)
			{
				foreach (HttpHeader header in context.Request.Headers)
					sb.Append(Environment.NewLine).Append('\t').Append(header.Key).Append(": ").Append(header.Value);
			}
			await WriteLogAsync(sb.ToString()).ConfigureAwait(false);

			if (Options.LogResponseStatus && context.RequestWillBeProxied)
			{
				string requestUrl = context.Request.Url.ToString();
				context.AddResponseHeadersHook(async (PluginResponseContext responseContext) =>
				{
					await WriteLogAsync(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\tRESPONSE " + responseContext.Response.StatusString + "\t" + requestUrl).ConfigureAwait(false);
				});
			}
			return PluginRequestAction.Continue;
		}
		private async Task WriteLogAsync(string line)
		{
			await writeLock.WaitAsync().ConfigureAwait(false);
			try
			{
				await File.AppendAllTextAsync(logFilePath, line + Environment.NewLine).ConfigureAwait(false);
			}
			finally
			{
				writeLock.Release();
			}
		}
	}
}

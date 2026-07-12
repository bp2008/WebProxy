using BPUtil;
using System;
using System.IO;
using WebProxy.Plugins;

namespace WebProxy
{
	/// <summary>
	/// WebProxy's implementation of <see cref="IPluginHost"/>, providing host services to one plugin instance.
	/// </summary>
	public class PluginHostImpl : IPluginHost
	{
		private static readonly Version hostVersion = Version.Parse(Globals.AssemblyVersion);
		private readonly string instanceId;
		private string dataDirectoryPath;
		/// <summary>
		/// Constructs a PluginHostImpl for the plugin instance with the given Id.
		/// </summary>
		/// <param name="instanceId">Id of the configured plugin instance.</param>
		public PluginHostImpl(string instanceId)
		{
			this.instanceId = instanceId;
		}
		/// <inheritdoc/>
		public void Log(string message)
		{
			Logger.Info("[Plugin " + instanceId + "] " + message);
		}
		/// <inheritdoc/>
		public void LogError(Exception ex, string additionalInformation = null)
		{
			string info = "[Plugin " + instanceId + "]" + (string.IsNullOrWhiteSpace(additionalInformation) ? "" : (" " + additionalInformation));
			WebProxyService.ReportError(ex, info);
		}
		/// <inheritdoc/>
		public string DataDirectoryPath
		{
			get
			{
				if (dataDirectoryPath == null)
				{
					string path = Path.Combine(Globals.WritableDirectoryBase, "PluginData", StringUtil.MakeSafeForFileName(instanceId));
					Directory.CreateDirectory(path);
					dataDirectoryPath = path;
				}
				return dataDirectoryPath;
			}
		}
		/// <inheritdoc/>
		public string ServerDataDirectoryPath => Globals.WritableDirectoryBase;
		/// <inheritdoc/>
		public Version HostVersion => hostVersion;
	}
}

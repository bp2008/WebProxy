using System;

namespace WebProxy.Plugins
{
	/// <summary>
	/// Provides host services to a plugin instance.  An implementation of this interface is assigned to <see cref="WebProxyPlugin.Host"/> before the plugin is loaded.
	/// </summary>
	public interface IPluginHost
	{
		/// <summary>
		/// Writes a message to WebProxy's application log, prefixed with the plugin instance Id.
		/// </summary>
		/// <param name="message">Message to log.</param>
		void Log(string message);
		/// <summary>
		/// Reports an error to WebProxy's application log (and error tracker, if configured), prefixed with the plugin instance Id.
		/// </summary>
		/// <param name="ex">Exception to report.</param>
		/// <param name="additionalInformation">Optional additional information to log with the exception.</param>
		void LogError(Exception ex, string additionalInformation = null);
		/// <summary>
		/// <para>Gets the full path of a writable directory reserved for this plugin instance, e.g. for storing state files.  The directory is created automatically when this property is first read.</para>
		/// <para>The path is specific to the plugin instance Id, so multiple instances of the same plugin get separate directories.</para>
		/// </summary>
		string DataDirectoryPath { get; }
		/// <summary>
		/// Gets the full path of WebProxy's data directory (the directory containing Settings.json).  Most plugins should prefer <see cref="DataDirectoryPath"/>.
		/// </summary>
		string ServerDataDirectoryPath { get; }
		/// <summary>
		/// Gets the version of the WebProxy host application.
		/// </summary>
		Version HostVersion { get; }
	}
}

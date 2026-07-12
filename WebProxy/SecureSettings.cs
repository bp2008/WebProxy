using BPUtil;
using System;

namespace WebProxy
{
	/// <summary>
	/// <para>WebProxy's secure settings file ("SecureSettings.json", stored in WebProxy's data folder next to "Settings.json").</para>
	/// <para>Unlike <see cref="Settings"/>, this file can not be imported or modified via the web-based Admin Console; it is intended to be managed only by an administrator with shell access to the machine running WebProxy (via manual file editing, the command line interface on Linux, or the Service Manager GUI on Windows).  Options in this file gate capabilities which are considered too dangerous for the web console to expose out-of-the-box.</para>
	/// <para>Because this file may be edited at any time by another process (the command line interface or Service Manager GUI run in a separate process from the service), consumers should call <see cref="GetCurrent"/> each time the settings are needed instead of caching the result.</para>
	/// </summary>
	public class SecureSettings : SerializableObjectJson
	{
		/// <summary>
		/// <para>If true, authenticated users of the web-based Admin Console are allowed to install and delete plugin files (DLLs).  Because plugins are .NET code which runs with the privileges of the WebProxy process, this capability is equivalent to granting remote code execution to everyone with web console access, so it is disabled by default.</para>
		/// <para>This flag only controls the ability to remotely add or remove plugin files.  Web console users can configure and use plugins that are already installed regardless of the state of this flag.</para>
		/// <para>This flag can not be enabled via the web console.  See PLUGINS.md for instructions.</para>
		/// </summary>
		public bool AllowRemotePluginFileManagement = false;

		protected override SerializableObjectJson DeserializeFromJson(string str)
		{
			return Newtonsoft.Json.JsonConvert.DeserializeObject<SecureSettings>(str);
		}

		protected override string SerializeToJson(object obj)
		{
			return Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
		}

		/// <summary>
		/// Reads the current SecureSettings from disk, returning default values if the file does not exist.  The file is re-read on every call because it may be edited at any time by an administrator (manually or via the command line interface / Service Manager GUI) while the service is running.
		/// </summary>
		/// <returns>The current SecureSettings.</returns>
		public static SecureSettings GetCurrent()
		{
			SecureSettings s = new SecureSettings();
			s.Load();
			return s;
		}
		/// <summary>
		/// Sets the value of <see cref="AllowRemotePluginFileManagement"/> and saves the SecureSettings file.
		/// </summary>
		/// <param name="allow">Value to assign to <see cref="AllowRemotePluginFileManagement"/>.</param>
		public static void SetAllowRemotePluginFileManagement(bool allow)
		{
			SecureSettings s = GetCurrent();
			s.AllowRemotePluginFileManagement = allow;
			s.Save();
		}
	}
}

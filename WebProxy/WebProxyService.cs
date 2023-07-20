using BPUtil;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	/// <summary>
	/// Singleton service class for WebProxy.
	/// </summary>
	public partial class WebProxyService : ServiceBase
	{
		/// <summary>
		/// Reference to the constructed WebProxyService instance. Null if none has been constructed yet.
		/// </summary>
		public static WebProxyService service;
		/// <summary>
		/// The web server.
		/// </summary>
		private static WebServer webServer = new WebServer();
		public WebProxyService()
		{
			if (service != null)
				throw new Exception("Unable to create WebProxyService because one was already created.");

			InitializeSettings();


			// These should not affect proxying because we use TcpClient and implement HTTP at a low level instead of any built-in high level HTTP client.
			// Nonetheless I'm setting them.
			if (!ServicePointManager.SecurityProtocol.HasFlag(SecurityProtocolType.Tls12))
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			if (ServicePointManager.DefaultConnectionLimit < 16)
				ServicePointManager.DefaultConnectionLimit = 16;

			InitializeComponent();

			webServer.pool.MaxThreads = 1024;
			service = this;
		}

		private static void InitializeSettings()
		{
			staticSettings = new Settings();
			staticSettings.Load();
			staticSettings.SaveIfNoExist();

			SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
		}
		public const string AdminConsoleLoginId = "WebProxy Admin Console Login";
		/// <summary>
		/// Validate setup and create/repair admin console access. Do not modify the objects returned via out parameters.
		/// </summary>
		/// <param name="adminConsoleEntrypoint">The Entrypoint used by the Admin Console.</param>
		/// <param name="adminConsoleExitpoint">The Exitpoint used by the Admin Console.</param>
		/// <param name="adminConsoleLogin">The authentication middleware used by the Admin Console.</param>
		public static void SettingsValidateAndAdminConsoleSetup(out Entrypoint adminConsoleEntrypoint, out Exitpoint adminConsoleExitpoint, out Middleware adminConsoleLogin)
		{
			// We shall follow the established thread-safe procedure for changing 
			//   the Settings while other threads are accessing Settings:
			// 1. Clone the settings object via CloneSettingsObjectSlow().
			// 2. Edit the clone.
			// 3. Commit the clone via SaveNewSettings().

			Settings s = CloneSettingsObjectSlow();
			bool shouldSave = false;

			if (s.entrypoints == null)
				s.entrypoints = new List<Entrypoint>();
			if (s.exitpoints == null)
				s.exitpoints = new List<Exitpoint>();
			if (s.middlewares == null)
				s.middlewares = new List<Middleware>();
			if (s.proxyRoutes == null)
				s.proxyRoutes = new List<ProxyRoute>();

			// Find or create the "WebProxy Admin Console Login" middleware.
			adminConsoleLogin = s.middlewares.FirstOrDefault(m => m.Id == AdminConsoleLoginId);
			if (adminConsoleLogin == null)
			{
				adminConsoleLogin = new Middleware();
				adminConsoleLogin.Id = AdminConsoleLoginId;
				adminConsoleLogin.Type = MiddlewareType.HttpDigestAuth;
				adminConsoleLogin.AuthCredentials = new List<UnPwCredential>();
				adminConsoleLogin.SetPassword("wpadmin", StringUtil.GetRandomAlphaNumericString(16));
				s.middlewares.Add(adminConsoleLogin);
				shouldSave = true;
			}

			// Find the Admin console exitpoint
			adminConsoleExitpoint = s.exitpoints.FirstOrDefault(e => e.type == ExitpointType.AdminConsole);
			if (adminConsoleExitpoint == null)
			{
				adminConsoleExitpoint = new Exitpoint();
				adminConsoleExitpoint.name = "WebProxy Admin Console";
				adminConsoleExitpoint.host = "*";
				adminConsoleExitpoint.middlewares = new string[] { adminConsoleLogin.Id };
				adminConsoleExitpoint.type = ExitpointType.AdminConsole;
				s.exitpoints.Add(adminConsoleExitpoint);
				shouldSave = true;
			}
			if (!adminConsoleExitpoint.middlewares.Contains(adminConsoleLogin.Id))
			{
				adminConsoleExitpoint.middlewares = adminConsoleExitpoint.middlewares.Concat(new string[] { adminConsoleLogin.Id }).ToArray();
				shouldSave = true;
			}

			// Find the first route to this exitpoint, and ensure that the entrypoint exists.
			string exitpointName = adminConsoleExitpoint.name;
			adminConsoleEntrypoint = null;
			ProxyRoute adminConsoleRoute = s.proxyRoutes.FirstOrDefault(r => r.exitpointName == exitpointName);
			if (adminConsoleRoute != null)
			{
				adminConsoleEntrypoint = s.entrypoints.FirstOrDefault(e => e.name == adminConsoleRoute.entrypointName);
				if (adminConsoleEntrypoint != null)
				{
					// Admin console route exists and so does the entrypoint.  The settings are fine.
				}
				else
				{
					// Entrypoint does not exist.  Delete the broken route so that we can restore it predictably.
					s.proxyRoutes.Remove(adminConsoleRoute);
					adminConsoleRoute = null;
					shouldSave = true;
				}
			}

			if (adminConsoleRoute == null)
			{
				// Create/repair admin console entrypoint and route.
				adminConsoleEntrypoint = s.entrypoints.FirstOrDefault(e => e.httpPort == 8080 || e.httpsPort == 8080);
				if (adminConsoleEntrypoint == null)
				{
					adminConsoleEntrypoint = new Entrypoint();
					adminConsoleEntrypoint.httpPort = 8080;
					adminConsoleEntrypoint.httpsPort = 8080;
					adminConsoleEntrypoint.ipAddress = null;
					adminConsoleEntrypoint.name = "WebProxy Admin Console Entrypoint";
					s.entrypoints.Add(adminConsoleEntrypoint);
				}

				adminConsoleRoute = new ProxyRoute();
				adminConsoleRoute.entrypointName = adminConsoleEntrypoint.name;
				adminConsoleRoute.exitpointName = adminConsoleExitpoint.name;
				s.proxyRoutes.Add(adminConsoleRoute);
				shouldSave = true;
			}

			// After all changes are made to the settings object, the settings can be saved.
			if (shouldSave)
				SaveNewSettings(s);
		}

		public static void ReportError(Exception ex)
		{
			Logger.Debug(ex);
		}

		protected override void OnStart(string[] args)
		{
			UpdateWebServerBindings();
		}

		/// <summary>
		/// Updates the web server bindings according to the current configuration.  It is safe to call this even if bindings have not changed.
		/// </summary>
		public static void UpdateWebServerBindings()
		{
			webServer.UpdateBindings();
		}

		protected override void OnStop()
		{
			webServer.Stop();
		}
		#region Settings
		/// <summary>
		/// <para>Static settings object. To retain maximum performance and exception safety without locks, some usage constraints are necessary:</para>
		/// <para>* To read the settings, call MakeLocalSettingsReference and store the returned value in a local variable.  Treat the fields/properties of the settings object as read-only.</para>
		/// <para>* To write/change anything in settings, make a local COPY of the settings object via CloneSettingsObjectSlow(), edit the copy, then pass it to SaveNewSettings().</para>
		/// </summary>
		private static Settings staticSettings;

		/// <summary>
		/// Returns a snapshot of the settings.  Store the returned value in a local variable and use it from there, because calling this method is not guaranteed to return the same object each time.  Failure to treat the returned object as read-only will yield race conditions and errors in other threads.
		/// </summary>
		/// <returns></returns>
		public static Settings MakeLocalSettingsReference()
		{
			return staticSettings;
		}
		/// <summary>
		/// Returns a detached copy of the settings.  You can modify the returned object and send it to SaveNewSettings().
		/// </summary>
		/// <returns></returns>
		public static Settings CloneSettingsObjectSlow()
		{
			return JsonConvert.DeserializeObject<Settings>(JsonConvert.SerializeObject(staticSettings));
		}
		private static object settingsSaveLock = new object();
		/// <summary>
		/// Replaces the internal settings object with this one and saves the settings to disk in a thread-safe manner.  You should not modify the settings object again after calling this; instead, make a new clone of the settings object if you need to make more changes.
		/// </summary>
		/// <param name="newSettings">A clone of the settings object.  The clone contains changes that you want to save.</param>
		public static void SaveNewSettings(Settings newSettings)
		{
			lock (settingsSaveLock)
			{
				staticSettings = newSettings;
				newSettings.Save();
			}
		}
		#endregion
		//#region Acme Account Key
		//private static object acmeAccountKeyLock = new object();
		//private static string acmeAccountKey;
		//public static string AcmeAccountKeyPersistent
		//{
		//	get
		//	{

		//	}
		//	set
		//	{
		//		lock (acmeAccountKeyLock)
		//		{
		//			Robust.Retry(() =>
		//			{
		//				File.WriteAllText(Globals.WritableDirectoryBase + "AcmeAccount.pem", acmeAccountKey, ByteUtil.Utf8NoBOM);
		//			}, 5, 10, 20, 40, 80, 160, 320, 640, 1280, 1280, 1280, 1280, 1280, 1280, 1280, 1280, 1280);
		//		}
		//	}
		//}
		//#endregion
	}
}

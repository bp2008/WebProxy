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
	public partial class WebProxyService
#if !LINUX
		: ServiceBase
#endif
	{
		/// <summary>
		/// Reference to the constructed WebProxyService instance. Null if none has been constructed yet.
		/// </summary>
		public static WebProxyService service;
		/// <summary>
		/// The web server.
		/// </summary>
		private static WebServer webServer;
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

#if !LINUX
			InitializeComponent();
#endif

			BasicErrorTracker.Initialize();
			Logger.CatchAll((source, ex) => ReportError(ex, source));

			webServer = new WebServer();
			webServer.pool.MaxThreads = 1024;
			service = this;
		}

		/// <summary>
		/// Loads the settings file, saves it if it does not exist, and then validates the settings and ensures the admin console is available.
		/// </summary>
		public static void InitializeSettings()
		{
			Settings s = new Settings();
			s.Load();
			string settingsOriginal = JsonConvert.SerializeObject(s);
			ValidateSettings(s);
			string settingsAfterValidation = JsonConvert.SerializeObject(s);
			lock (settingsSaveLock)
			{
				if (settingsOriginal != settingsAfterValidation)
					SaveNewSettings(s);
				else
				{
					staticSettings = s;
					staticSettings.SaveIfNoExist();
				}

				SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
			}
		}
		/// <summary>
		/// Logs the Exception.
		/// </summary>
		/// <param name="ex">Exception to log.</param>
		public static void ReportError(Exception ex)
		{
			Logger.Debug(ex);
			BasicErrorTracker.GenericError(ex.ToHierarchicalString());
		}

		/// <summary>
		/// Logs the Exception.
		/// </summary>
		/// <param name="ex">Exception to log.</param>
		/// <param name="additionalInformation">Optional additional information to log with the exception.</param>
		public static void ReportError(Exception ex, string additionalInformation)
		{
			Logger.Debug(ex, additionalInformation);
			string msg = string.IsNullOrWhiteSpace(additionalInformation) ? ex.ToHierarchicalString() : (additionalInformation + Environment.NewLine + ex.ToHierarchicalString());
			BasicErrorTracker.GenericError(msg);
		}
		/// <summary>
		/// Logs the Exception.
		/// </summary>
		/// <param name="message">Message to log.</param>
		public static void ReportError(string message)
		{
			Logger.Debug(message);
			BasicErrorTracker.GenericError(message);
		}

#if LINUX
		protected void OnStart(string[] args)
		{
			Logger.Info(Globals.AssemblyName + " Starting Up ");
			UpdateWebServerBindings();
		}

		protected void OnStop()
		{
			webServer.Stop();
			Logger.Info(Globals.AssemblyName + " Shutting Down");
		}
#else
		protected override void OnStart(string[] args)
		{
			Logger.Info(Globals.AssemblyName + " Starting Up ");
			UpdateWebServerBindings();
		}

		protected override void OnStop()
		{
			webServer.Stop();
			Logger.Info(Globals.AssemblyName + " Shutting Down");
		}
#endif

		/// <summary>
		/// Updates the web server bindings according to the current configuration.  It is safe to call this even if bindings have not changed.
		/// </summary>
		public static void UpdateWebServerBindings()
		{
			webServer.UpdateBindings();
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
			ValidateSettings(newSettings);

			lock (settingsSaveLock)
			{
				staticSettings = newSettings;
				newSettings.Save();
			}

			string settingsBackupDir = Path.Combine(Globals.WritableDirectoryBase, "SettingsBackup");
			Directory.CreateDirectory(settingsBackupDir);
			string settingsBackupPath = Path.Combine(settingsBackupDir, "SettingsBackup.zip");
			string backupJsonFilename = "Settings-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff") + ".json";
			string json = JsonConvert.SerializeObject(newSettings);
			byte[] fileBody = ByteUtil.Utf8NoBOM.GetBytes(json);
			try
			{
				Compression.AddFileToZip(settingsBackupPath, backupJsonFilename, fileBody);
			}
			catch (Exception ex)
			{
				ReportError(ex);
			}
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
			if (string.IsNullOrWhiteSpace(adminConsoleExitpoint.name))
			{
				adminConsoleExitpoint.name = "WebProxy Admin Console";
				shouldSave = true;
			}
			if (string.IsNullOrWhiteSpace(adminConsoleExitpoint.host))
			{
				adminConsoleExitpoint.host = "*";
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
					// Admin console route exists and so does the entrypoint.
					if (!adminConsoleEntrypoint.httpPortValid() && !adminConsoleEntrypoint.httpsPortValid())
					{
						adminConsoleEntrypoint.httpPort = 8080;
						adminConsoleEntrypoint.httpsPort = 8080;
						adminConsoleEntrypoint.ipAddress = null;
						shouldSave = true;
					}
					if (string.IsNullOrWhiteSpace(adminConsoleEntrypoint.name))
					{
						adminConsoleEntrypoint.name = "WebProxy Admin Console Entrypoint";
						shouldSave = true;
					}
					// Currently we do not attempt to repair bad IP Address bindings.
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
		/// <summary>
		/// Validate the settings file and repair simple problems.  Throw an exception if anything is invalid that can't be cleanly repaired automatically.  Because this can modify the settings, this should never be passed the static settings instance, and should only be called just prior to saving the settings.
		/// </summary>
		/// <param name="s">Settings instance containing settings that need to be validated.</param>
		/// <exception cref="Exception">If validation fails.</exception>
		private static void ValidateSettings(Settings s)
		{
			if (s == staticSettings)
				throw new Exception("Application error: Refusing to run ValidateSettings on the static settings instance due to causing race conditions.");

			if (s.entrypoints == null)
				s.entrypoints = new List<Entrypoint>();
			if (s.exitpoints == null)
				s.exitpoints = new List<Exitpoint>();
			if (s.middlewares == null)
				s.middlewares = new List<Middleware>();
			if (s.proxyRoutes == null)
				s.proxyRoutes = new List<ProxyRoute>();

			HashSet<string> nameUniqueness = new HashSet<string>();

			// Validate Entrypoints
			foreach (Entrypoint entrypoint in s.entrypoints)
			{
				if (entrypoint == null)
					throw new Exception("Exitpoint is null.");

				if (string.IsNullOrWhiteSpace(entrypoint.name))
					throw new Exception("Entrypoint index " + s.entrypoints.IndexOf(entrypoint) + " does not have a name.");

				if (entrypoint.middlewares == null)
					entrypoint.middlewares = new string[0];

				for (int i = 0; i < entrypoint.middlewares.Length; i++)
					entrypoint.middlewares[i] = entrypoint.middlewares[i].Trim();

				entrypoint.name = entrypoint.name.Trim();
				if (nameUniqueness.Contains(entrypoint.name.ToLower()))
					throw new Exception("Entrypoint names are not unique. Duplicate name: \"" + entrypoint.name + "\"");
				nameUniqueness.Add(entrypoint.name.ToLower());
			}

			nameUniqueness.Clear();

			// Validate Exitpoints
			foreach (Exitpoint exitpoint in s.exitpoints)
			{
				if (exitpoint == null)
					throw new Exception("Exitpoint is null.");

				if (string.IsNullOrWhiteSpace(exitpoint.name))
					throw new Exception("Exitpoint index " + s.exitpoints.IndexOf(exitpoint) + " does not have a name.");

				if (exitpoint.middlewares == null)
					exitpoint.middlewares = new string[0];

				for (int i = 0; i < exitpoint.middlewares.Length; i++)
					exitpoint.middlewares[i] = exitpoint.middlewares[i].Trim();

				if (exitpoint.type == ExitpointType.AdminConsole || exitpoint.type == ExitpointType.WebProxy)
				{
					if (exitpoint.autoCertificate && string.IsNullOrWhiteSpace(s.acmeAccountEmail))
						throw new Exception("Exitpoint \"" + exitpoint.name + "\" is not allowed to use automatic certificate management because the LetsEncrypt Account Email field is not assigned in Global Settings.");

					if (exitpoint.certificatePath == null)
						exitpoint.certificatePath = "";

					if (!string.IsNullOrWhiteSpace(exitpoint.certificatePath))
					{
						try
						{
							FileInfo fi = new FileInfo(exitpoint.certificatePath);
						}
						catch (Exception ex)
						{
							throw new Exception("Exitpoint \"" + exitpoint.name + "\" has invalid Certificate Path.", ex);
						}
					}
				}

				if (exitpoint.type == ExitpointType.WebProxy)
				{
					if (exitpoint.destinationOrigin == null || !Uri.TryCreate(exitpoint.destinationOrigin, UriKind.Absolute, out Uri ignored))
						throw new Exception("Exitpoint \"" + exitpoint.name + "\" has invalid Destination Origin.");

					if (!string.IsNullOrEmpty(exitpoint.destinationHostHeader) && !Uri.TryCreate("http://" + exitpoint.destinationHostHeader + ":80/", UriKind.Absolute, out Uri ignored2))
						throw new Exception("Exitpoint \"" + exitpoint.name + "\" has invalid Destination Host Header.");
				}

				exitpoint.name = exitpoint.name.Trim();
				if (nameUniqueness.Contains(exitpoint.name.ToLower()))
					throw new Exception("Exitpoint names are not unique. Duplicate name: \"" + exitpoint.name + "\"");
				nameUniqueness.Add(exitpoint.name.ToLower());
			}

			nameUniqueness.Clear();

			// Validate Middlewares
			foreach (Middleware middleware in s.middlewares)
			{
				if (middleware == null)
					throw new Exception("Middleware is null.");

				if (string.IsNullOrWhiteSpace(middleware.Id))
					throw new Exception("Middleware index " + s.middlewares.IndexOf(middleware) + " does not have a name.");

				middleware.Id = middleware.Id.Trim();
				if (nameUniqueness.Contains(middleware.Id.ToLower()))
					throw new Exception("Middleware names are not unique. Duplicate name: \"" + middleware.Id + "\"");
				nameUniqueness.Add(middleware.Id.ToLower());

				if (middleware.Type == MiddlewareType.IPWhitelist)
				{
					foreach (string range in middleware.WhitelistedIpRanges)
					{
						try
						{
							IPAddressRange ipr = new IPAddressRange(range);
						}
						catch (Exception ex)
						{
							throw new Exception("Middleware \"" + middleware.Id + "\" defines invalid IPAddressRange \"" + range + "\"", ex);
						}
					}
				}

				if (middleware.Type == MiddlewareType.HttpDigestAuth)
				{
					foreach (UnPwCredential c in middleware.AuthCredentials)
					{
						if (string.IsNullOrWhiteSpace(c.User))
							throw new Exception("Middleware \"" + middleware.Id + "\" defines invalid credential (missing username)");
						if (string.IsNullOrEmpty(c.Pass))
							throw new Exception("Middleware \"" + middleware.Id + "\" defines invalid credential (missing password)");
					}
				}

				if (middleware.Type == MiddlewareType.AddHttpHeaderToResponse)
				{
					HttpHeaderCollection collection = new HttpHeaderCollection();
					try
					{
						collection.AssignHeaderFromString(middleware.HttpHeader);
					}
					catch (Exception ex)
					{
						throw new Exception("Middleware \"" + middleware.Id + "\" failed HTTP header validation.", ex);
					}
				}
			}

			nameUniqueness.Clear();

			// Validate ProxyRoutes
			foreach (ProxyRoute r in s.proxyRoutes)
			{
				if (r == null)
					throw new Exception("ProxyRoute is null.");

				if (string.IsNullOrWhiteSpace(r.entrypointName))
					throw new Exception("ProxyRoute index " + s.proxyRoutes.IndexOf(r) + " does not specify an Entrypoint.");
				if (string.IsNullOrWhiteSpace(r.exitpointName))
					throw new Exception("ProxyRoute index " + s.proxyRoutes.IndexOf(r) + " does not specify an Exitpoint.");

				r.entrypointName = r.entrypointName.Trim();
				r.exitpointName = r.exitpointName.Trim();

				if (!s.entrypoints.Any(e => e.name == r.entrypointName))
					throw new Exception("ProxyRoute index " + s.proxyRoutes.IndexOf(r) + " specifies non-existent Entrypoint named \"" + r.entrypointName + "\".");
				if (!s.exitpoints.Any(e => e.name == r.exitpointName))
					throw new Exception("ProxyRoute index " + s.proxyRoutes.IndexOf(r) + " specifies non-existent Exitpoint named \"" + r.exitpointName + "\".");

				string routeId = "[" + r.entrypointName + "] -> [" + r.exitpointName + "]";
				if (nameUniqueness.Contains(routeId.ToLower()))
					throw new Exception("ProxyRoutes are not unique. Duplicate ProxyRoutes: \"" + routeId + "\"");
				nameUniqueness.Add(routeId.ToLower());
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

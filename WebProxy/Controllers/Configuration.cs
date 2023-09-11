using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebProxy.LetsEncrypt;

namespace WebProxy.Controllers
{
	public class Configuration : AdminConsoleControllerBase
	{
		public Task<ActionResult> Get()
		{
			return Get(false);
		}
		private async Task<ActionResult> Get(bool withEntryLinks)
		{
			Settings s = WebProxyService.MakeLocalSettingsReference();
			GetConfigurationResponse response = new GetConfigurationResponse();
#if LINUX
			response.memoryMax = await AppInit.GetPropertySystemdServiceFileAsync(Program.serviceName, "MemoryMax", CancellationToken).ConfigureAwait(false);
#endif
			await Task.CompletedTask.ConfigureAwait(false);
			response.acmeAccountEmail = s.acmeAccountEmail;
			response.entrypoints = s.entrypoints.ToArray();
			response.exitpoints = s.exitpoints.ToArray();
			response.middlewares = s.middlewares.ToArray();
			response.proxyRoutes = s.proxyRoutes.ToArray();
			response.errorTrackerSubmitUrl = s.errorTrackerSubmitUrl;
			response.cloudflareApiToken = s.cloudflareApiToken;
			response.verboseWebServerLogs = s.verboseWebServerLogs;
			response.serverMaxConnectionCount = s.serverMaxConnectionCount;

			if (withEntryLinks)
			{
				WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
				string adminHost = adminExit.host;
				adminHost = adminHost?.Replace("*", "");
				if (string.IsNullOrEmpty(adminHost))
					adminHost = Context.httpProcessor.HostName;

				List<string> adminEntryOrigins = new List<string>();
				if (adminEntry.httpPortValid())
					adminEntryOrigins.Add("http://" + adminHost + (adminEntry.httpPort == 80 ? "" : (":" + adminEntry.httpPort)));
				if (adminEntry.httpsPortValid())
					adminEntryOrigins.Add("https://" + adminHost + (adminEntry.httpsPort == 443 ? "" : (":" + adminEntry.httpsPort)));

				response.adminEntryOrigins = adminEntryOrigins.ToArray();
			}
			return ApiSuccessNoAutocomplete(response);
		}
		public async Task<ActionResult> Set()
		{
			SetConfigurationRequest request = await ParseRequest<SetConfigurationRequest>().ConfigureAwait(false);

			Settings s = WebProxyService.CloneSettingsObjectSlow();
			s.acmeAccountEmail = request.acmeAccountEmail;
			s.entrypoints = request.entrypoints.ToList();
			s.exitpoints = request.exitpoints.ToList();
			s.middlewares = request.middlewares.ToList();
			s.proxyRoutes = request.proxyRoutes.ToList();
			s.errorTrackerSubmitUrl = request.errorTrackerSubmitUrl;
			s.cloudflareApiToken = request.cloudflareApiToken;
			s.verboseWebServerLogs = request.verboseWebServerLogs;
			s.serverMaxConnectionCount = request.serverMaxConnectionCount;

			return await Set(s).ConfigureAwait(false);
		}
		private async Task<ActionResult> Set(Settings s)
		{
			try
			{
				await WebProxyService.SaveNewSettings(s).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				return ApiError(ex.FlattenMessages());
			}

			return await Get(true).ConfigureAwait(false);
		}
		public async Task<ActionResult> ForceRenew()
		{
			ForceRenewRequest request = await ParseRequest<ForceRenewRequest>().ConfigureAwait(false);

			Settings s = WebProxyService.MakeLocalSettingsReference();
			Exitpoint exitpoint = s.exitpoints.FirstOrDefault(e => e.name == request.forceRenewExitpointName);

			if (exitpoint == null)
				return ApiError("Unable to find the chosen exitpoint.");

			foreach (ProxyRoute route in s.proxyRoutes.Where(r => r.exitpointName == exitpoint.name))
			{
				Entrypoint entrypoint = s.entrypoints.FirstOrDefault(e => e.name == route.entrypointName);
				if (entrypoint != null)
				{
					if (entrypoint.httpPort == 80 || entrypoint.httpsPort == 443)
					{
						string result = await CertMgr.ForceRenew(entrypoint, exitpoint).ConfigureAwait(false);
						if (string.IsNullOrEmpty(result))
							return ApiSuccessNoAutocomplete(new ApiResponseBase(true));
						return ApiError(result);
					}
				}
			}
			return ApiError("Unable to find an acceptable Entrypoint that is routed to the chosen Exitpoint.");
		}
		[RequiresHttpMethod("GET")]
		public Task<ActionResult> GetRaw()
		{
			return PlainTextTask(JsonConvert.SerializeObject(WebProxyService.MakeLocalSettingsReference(), Formatting.Indented));
		}
		public async Task<ActionResult> UploadCertificate()
		{
			UploadCertificateRequest request = await ParseRequest<UploadCertificateRequest>().ConfigureAwait(false);

			Settings s = WebProxyService.MakeLocalSettingsReference();
			Exitpoint exitpoint = s.exitpoints.FirstOrDefault(e => e.name == request.exitpointName);

			if (exitpoint == null)
				return ApiError("Unable to find the chosen exitpoint.");

			byte[] certBytes = null;
			X509Certificate2 cert;
			try
			{
				certBytes = Base64UrlMod.FromBase64UrlMod(request.certificateBase64);
				cert = new X509Certificate2(certBytes);
			}
			catch (Exception ex)
			{
				return ApiError("Invalid data was uploaded: " + ex.Message);
			}


			string certPath = exitpoint.certificatePath;
			if (string.IsNullOrWhiteSpace(certPath))
			{
				string[] domains = exitpoint.getAllDomains();
				if (domains.Length == 0)
					return ApiError("This exitpoint does not have any domains configured, so a certificate path can't be automatically generated yet.");
				Settings newSettings = WebProxyService.CloneSettingsObjectSlow();
				exitpoint = newSettings.exitpoints.First(e => e.name == exitpoint.name);
				exitpoint.certificatePath = certPath = CertMgr.GetDefaultCertificatePath(domains[0]);
				await WebProxyService.SaveNewSettings(newSettings).ConfigureAwait(false);
			}

			DirectoryInfo diCertDir = new FileInfo(certPath).Directory;
			if (!diCertDir.Exists)
			{
				await Robust.RetryPeriodicAsync(() =>
				{
					Directory.CreateDirectory(diCertDir.FullName);
					return Task.CompletedTask;
				}, 50, 6, CancellationToken).ConfigureAwait(false);
			}

			await Robust.RetryPeriodicAsync(async () =>
			{
				await File.WriteAllBytesAsync(certPath, certBytes, CancellationToken).ConfigureAwait(false);
			}, 50, 6, CancellationToken).ConfigureAwait(false);

			return await Get(false).ConfigureAwait(false);
		}
		public async Task<ActionResult> TestCloudflareDNS()
		{
			BasicEventTimer bet = new BasicEventTimer();
			bet.Start("Get domain name");
			try
			{
				string anyDomainName = await CloudflareDnsValidator.GetAnyConfiguredDomain().ConfigureAwait(false);
				if (string.IsNullOrWhiteSpace(anyDomainName))
					return ApiError("Test failed: Found null or whitespace domain name in Cloudflare account.");

				bet.Start("Create DNS record");
				string key = "_test-access-token." + anyDomainName;
				string value = TimeUtil.GetTimeInMsSinceEpoch().ToString();

				await CloudflareDnsValidator.CreateDNSRecord(key, value).ConfigureAwait(false);

				bet.Start("Delete DNS record");
				int deleted = await CloudflareDnsValidator.DeleteDNSRecord(key).ConfigureAwait(false);

				bet.Stop();

				if (deleted > 0)
					return ApiSuccessNoAutocomplete(new ApiResponseBase(true));
				else
					return ApiError("Unable to delete test DNS record because it did not exist after successful creation.");
			}
			catch (Exception ex)
			{
				bet.Stop();
				return ApiError(ex.ToHierarchicalString());
			}
			finally
			{
				Context.ResponseHeaders["Server-Timing"] = bet.ToServerTimingHeader();
			}
		}
		[RequiresHttpMethod("GET")]
		public ActionResult Export()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (ZipArchive zipArchive = new ZipArchive(ms, ZipArchiveMode.Update, true))
				{
					// Write Settings
					Settings settings = WebProxyService.CloneSettingsObjectSlow();
					foreach (Exitpoint exitpoint in settings.exitpoints)
					{
						string pathNormalized = exitpoint.certificatePath.Replace('\\', '/');
						if (Platform.IsUnix() ? pathNormalized.StartsWith(CertMgr.CertsBaseDir) : pathNormalized.IStartsWith(CertMgr.CertsBaseDir))
						{
							// Replace Certs directory path with macro.  The macro will be expanded back into a path in the ValidateSettings method.
							exitpoint.certificatePath = "%CertsBaseDir%/" + exitpoint.certificatePath.Substring(CertMgr.CertsBaseDir.Length);
						}
					}
					string settingsJson = JsonConvert.SerializeObject(settings, Formatting.Indented);
					zipArchive.CreateEntryFromByteArray("Settings.json", ByteUtil.Utf8NoBOM.GetBytes(settingsJson), CompressionLevel.SmallestSize);

					// Write Cert Renewal Dates
					string certRenewalDatesJson = JsonConvert.SerializeObject(WebProxyService.certRenewalDates, Formatting.Indented);
					zipArchive.CreateEntryFromByteArray("CertRenewalDates.json", ByteUtil.Utf8NoBOM.GetBytes(certRenewalDatesJson), CompressionLevel.SmallestSize);

					// Write Certs
					DirectoryInfo certsDir = new DirectoryInfo(CertMgr.CertsBaseDir);
					if (certsDir.Exists)
					{
						foreach (FileInfo file in certsDir.GetFiles())
						{
							zipArchive.CreateEntryFromFile(file.FullName, "Certs/" + file.Name);
						}
					}
					Context.ResponseHeaders.Add("Content-Disposition", "attachment; filename=\"WebProxy_Export_" + StringUtil.MakeSafeForFileName(Context.httpProcessor.HostName) + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".zip\"");
				}
				return new FileDownloadResult(ms.ToArray(), false);
			}
		}
		public async Task<ActionResult> Import()
		{
			UploadSettingsAndCertificatesRequest request = await ParseRequest<UploadSettingsAndCertificatesRequest>().ConfigureAwait(false);

			byte[] zipBytes = null;
			try
			{
				zipBytes = Base64UrlMod.FromBase64UrlMod(request.base64);
			}
			catch (Exception ex)
			{
				return ApiError("Invalid data was uploaded: " + ex.Message);
			}

			// Read the archive
			Settings settings = null;
			ZipArchiveEntryData certRenewalData = null;
			List<ZipArchiveEntryData> certs = new List<ZipArchiveEntryData>();
			try
			{
				using (MemoryStream msInput = new MemoryStream(zipBytes))
				using (ZipArchive zipArchive = new ZipArchive(msInput, ZipArchiveMode.Read))
				{
					foreach (ZipArchiveEntry entry in zipArchive.Entries)
					{
						ZipArchiveEntryData entryData = entry.ExtractToObject();
						if (entryData.RelativePath == "Settings.json") // Read Settings
						{
							settings = JsonConvert.DeserializeObject<Settings>(ByteUtil.Utf8NoBOM.GetString(entryData.Data));
							WebProxyService.ValidateSettings(settings);
						}
						else if (entryData.RelativePath == "CertRenewalDates.json") // Read Cert Renewal Dates
							certRenewalData = entryData;
						else if (entryData.RelativePath.StartsWith("Certs/")) // Read Certs
							certs.Add(entryData);
					}
				}
			}
			catch (Exception ex)
			{
				return ApiError("The archive you are trying to import is not valid: " + ex.ToHierarchicalString());
			}
			if (settings == null && certRenewalData == null && certs.Count == 0)
				return ApiError("The archive you are trying to import did not contain any useful files.");

			// Commit the import.
			// Write certs
			if (certs.Count > 0)
			{
				foreach (ZipArchiveEntryData cert in certs)
				{
					string path = Path.Combine(Globals.WritableDirectoryBase, cert.RelativePath);
					FileInfo fi = new FileInfo(path);
					await Robust.RetryPeriodicAsync(async () =>
					{
						Directory.CreateDirectory(fi.Directory.FullName);
						await File.WriteAllBytesAsync(path, cert.Data, CancellationToken).ConfigureAwait(false);
						File.SetLastWriteTime(path, cert.LastWriteTime);
					}, 50, 10, CancellationToken).ConfigureAwait(false);
				}
			}
			// Write Cert Renewal Dates
			if (certRenewalData != null)
			{
				await Robust.RetryPeriodicAsync(async () =>
				{
					await File.WriteAllBytesAsync("CertRenewalDates.json", certRenewalData.Data, CancellationToken).ConfigureAwait(false);
				}, 50, 10, CancellationToken).ConfigureAwait(false);
			}
			// Write Settings
			return await Set(settings).ConfigureAwait(false);
		}
		public async Task<ActionResult> EnableServerGC()
		{
			await SetGarbageCollectorModeAsync(true, CancellationToken).ConfigureAwait(false);
			if (RestartServer())
				return this.ApiSuccessNoAutocomplete(new ApiResponseBase(true));
			return ApiError("The change will take effect when the service is restarted.");
		}
		public async Task<ActionResult> DisableServerGC()
		{
			await SetGarbageCollectorModeAsync(false, CancellationToken).ConfigureAwait(false);
			if (RestartServer())
				return this.ApiSuccessNoAutocomplete(new ApiResponseBase(true));
			return ApiError("The change will take effect when the service is restarted.");
		}

		/// <summary>
		/// Asynchronously sets the garbage collector type for the currently executing program by editing the service startup options.  The change affects future processes, not the one currently executing.
		/// </summary>
		/// <param name="useServerGC">If true, sets the garbage collector type to Server; otherwise, sets it to Workstation.</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns>A task that represents the asynchronous write operation.</returns>
		private async Task SetGarbageCollectorModeAsync(bool useServerGC, CancellationToken cancellationToken)
		{
#if LINUX
			await AppInit.SetEnvironmentVariableInSystemdServiceFileAsync(Program.serviceName, "DOTNET_gcServer", useServerGC ? "1" : "0", cancellationToken).ConfigureAwait(false);
			await TaskHelper.RunBlockingCodeSafely(() => AppInit.RunSystemctlDaemonReload(), cancellationToken).ConfigureAwait(false);
#else
			AppInit.SetEnvironmentVariableInWindowsService(Program.serviceName, "DOTNET_gcServer", useServerGC ? "1" : "0");
			await TaskHelper.CompletedTask.ConfigureAwait(false);
#endif
		}
		//		/// <summary>
		//		/// Asynchronously sets the garbage collector type for the currently executing program by writing a runtime configuration file.  The change affects future processes, not the one currently executing.
		//		/// </summary>
		//		/// <param name="useServerGC">If true, sets the garbage collector type to Server; otherwise, sets it to Workstation.</param>
		//		/// <param name="cancellationToken">Cancellation Token</param>
		//		/// <returns>A task that represents the asynchronous write operation.</returns>
		//		private Task SetGarbageCollectorModeAsync(bool useServerGC, CancellationToken cancellationToken)
		//		{
		//			string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
		//<configuration>
		//  <runtime>
		//    <gcServer enabled=""" + useServerGC.ToString().ToLower() + @"""/>
		//  </runtime>
		//</configuration>";
		//			string configFile = Assembly.GetEntryAssembly().Location + ".config";
		//			return File.WriteAllTextAsync(configFile, config, ByteUtil.Utf8NoBOM, cancellationToken);
		//		}
		//private async Task SetGarbageCollectorModeAsync(bool useServerGC, CancellationToken cancellationToken)
		//{
		//	string projectName = Assembly.GetExecutingAssembly().GetName().Name;
		//	string path = AppDomain.CurrentDomain.BaseDirectory + projectName + ".runtimeconfig.json";
		//	JObject config;

		//	if (File.Exists(path))
		//	{
		//		string json = await File.ReadAllTextAsync(path, ByteUtil.Utf8NoBOM, cancellationToken).ConfigureAwait(false);
		//		config = JObject.Parse(json);
		//	}
		//	else
		//	{
		//		config = new JObject();
		//		config["runtimeOptions"] = new JObject();
		//		config["runtimeOptions"]["configProperties"] = new JObject();
		//	}

		//	config["runtimeOptions"]["configProperties"]["System.GC.Server"] = useServerGC;
		//	await File.WriteAllTextAsync(path, config.ToString(), ByteUtil.Utf8NoBOM, cancellationToken).ConfigureAwait(false);
		//}
		public async Task<ActionResult> SetMemoryMaxMiB()
		{
			SetMemoryMaxMiBRequest request = await ParseRequest<SetMemoryMaxMiBRequest>().ConfigureAwait(false);
#if LINUX
			await AppInit.SetMemoryMaxInSystemdServiceFileAsync(Program.serviceName, request.MiB, CancellationToken).ConfigureAwait(false);
			await TaskHelper.RunBlockingCodeSafely(() => AppInit.RunSystemctlDaemonReload(), CancellationToken).ConfigureAwait(false);
			if (RestartServer())
				return this.ApiSuccessNoAutocomplete(new ApiResponseBase(true));
			return ApiError("The change will take effect when the service is restarted.");
#else
			return ApiError("Unsupported platform. This operation is only supported on Linux.");
#endif
		}
		private bool RestartServer()
		{
			if (Debugger.IsAttached)
				return false;
			Thread thrRestartSelf = new Thread(() =>
			{
				try
				{
					Thread.Sleep(1000);

#if LINUX
					AppInit.RestartLinuxSystemdService(Program.serviceName);
#else
					string restartBat = Globals.WritableDirectoryBase + "RestartService.bat";
					File.WriteAllText(restartBat, "NET STOP \"" + Program.serviceName + "\"" + Environment.NewLine + "NET START \"" + Program.serviceName + "\"");

					ProcessStartInfo psi = new ProcessStartInfo(restartBat, "");
					psi.UseShellExecute = true;
					psi.CreateNoWindow = true;
					Process.Start(psi);
#endif
				}
				catch (Exception ex)
				{
					Logger.Debug(ex, "WebProxy.Configuration.RestartServer");
				}
			});
			thrRestartSelf.Name = "Restart Self";
			thrRestartSelf.IsBackground = true;
			thrRestartSelf.Start();
			return true;
		}
	}
	public class GetConfigurationResponse : ApiResponseBase
	{
		public string appVersion = Globals.AssemblyVersion;
		public string acmeAccountEmail;
		public string errorTrackerSubmitUrl;
		public string cloudflareApiToken;
		public bool verboseWebServerLogs;
		public int serverMaxConnectionCount;
		public Entrypoint[] entrypoints;
		public Exitpoint[] exitpoints;
		public Middleware[] middlewares;
		public ProxyRoute[] proxyRoutes;
		public string[] exitpointTypes = Enum.GetNames(typeof(ExitpointType));
		public string[] middlewareTypes = Enum.GetNames(typeof(MiddlewareType));
		public string[] proxyHeaderBehaviorOptions = Enum.GetNames(typeof(BPUtil.SimpleHttp.Client.ProxyHeaderBehavior));
		public Dictionary<string, string> proxyHeaderBehaviorOptionsDescriptions = DescriptionAttribute.GetDescriptions<BPUtil.SimpleHttp.Client.ProxyHeaderBehavior>();
		public string[] tlsCipherSuiteSets = Enum.GetNames(typeof(TlsCipherSuiteSet));
		public Dictionary<string, string> tlsCipherSuiteSetDescriptions = DescriptionAttribute.GetDescriptions<TlsCipherSuiteSet>();
		public bool tlsCipherSuitesPolicySupported = BPUtil.SimpleHttp.HttpServer.IsTlsCipherSuitesPolicySupported();
		public LogFile[] logFiles = GetLogFiles();
		public bool gcModeServer = GCSettings.IsServerGC;
#if LINUX
		public bool platformSupportsMemoryMax = true;
		public string memoryMax;
#endif

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string[] adminEntryOrigins;

		public GetConfigurationResponse() : base(true, null)
		{
		}

		private static LogFile[] GetLogFiles()
		{
			DirectoryInfo di = new DirectoryInfo(Globals.WritableDirectoryBase + "Logs");
			return di.GetFiles("*.txt").Select(fi => new LogFile(fi.Name, fi.Length)).ToArray();
		}
	}
	public class LogFile
	{
		public string fileName;
		public string size;
		public LogFile() { }

		public LogFile(string fileName, long length)
		{
			this.fileName = fileName;
			this.size = StringUtil.FormatDiskBytes(length);
		}
	}
	public class SetConfigurationRequest
	{
		public string acmeAccountEmail;
		public string errorTrackerSubmitUrl;
		public string cloudflareApiToken;
		public bool verboseWebServerLogs;
		public int serverMaxConnectionCount;
		public Entrypoint[] entrypoints;
		public Exitpoint[] exitpoints;
		public Middleware[] middlewares;
		public ProxyRoute[] proxyRoutes;
	}
	public class ForceRenewRequest
	{
		public string forceRenewExitpointName;
	}
	public class UploadCertificateRequest
	{
		public string exitpointName;
		public string certificateBase64;
	}
	public class UploadSettingsAndCertificatesRequest
	{
		public string base64;
	}
	public class SetMemoryMaxMiBRequest
	{
		public uint? MiB;
	}
}

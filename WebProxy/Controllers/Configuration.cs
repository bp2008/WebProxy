using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using WebProxy.LetsEncrypt;

namespace WebProxy.Controllers
{
	public class Configuration : AdminConsoleControllerBase
	{
		public ActionResult Get()
		{
			return Get(false);
		}
		public ActionResult Set()
		{
			SetConfigurationRequest request = ParseRequest<SetConfigurationRequest>();

			Settings s = WebProxyService.CloneSettingsObjectSlow();
			s.acmeAccountEmail = request.acmeAccountEmail;
			s.entrypoints = request.entrypoints.ToList();
			s.exitpoints = request.exitpoints.ToList();
			s.middlewares = request.middlewares.ToList();
			s.proxyRoutes = request.proxyRoutes.ToList();
			s.errorTrackerSubmitUrl = request.errorTrackerSubmitUrl;
			s.cloudflareApiToken = request.cloudflareApiToken;
			s.verboseWebServerLogs = request.verboseWebServerLogs;

			return Set(s);
		}
		private ActionResult Get(bool withEntryLinks)
		{
			Settings s = WebProxyService.MakeLocalSettingsReference();
			GetConfigurationResponse response = new GetConfigurationResponse();
			response.acmeAccountEmail = s.acmeAccountEmail;
			response.entrypoints = s.entrypoints.ToArray();
			response.exitpoints = s.exitpoints.ToArray();
			response.middlewares = s.middlewares.ToArray();
			response.proxyRoutes = s.proxyRoutes.ToArray();
			response.errorTrackerSubmitUrl = s.errorTrackerSubmitUrl;
			response.cloudflareApiToken = s.cloudflareApiToken;
			response.verboseWebServerLogs = s.verboseWebServerLogs;

			if (withEntryLinks)
			{
				WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);
				string adminHost = adminExit.host;
				adminHost = adminHost?.Replace("*", "");
				if (string.IsNullOrEmpty(adminHost))
					adminHost = Context.httpProcessor.hostName;

				List<string> adminEntryOrigins = new List<string>();
				if (adminEntry.httpPortValid())
					adminEntryOrigins.Add("http://" + adminHost + (adminEntry.httpPort == 80 ? "" : (":" + adminEntry.httpPort)));
				if (adminEntry.httpsPortValid())
					adminEntryOrigins.Add("https://" + adminHost + (adminEntry.httpsPort == 443 ? "" : (":" + adminEntry.httpsPort)));

				response.adminEntryOrigins = adminEntryOrigins.ToArray();
			}
			return ApiSuccessNoAutocomplete(response);
		}
		private ActionResult Set(Settings s)
		{
			try
			{
				WebProxyService.SaveNewSettings(s);
			}
			catch (Exception ex)
			{
				return ApiError(ex.FlattenMessages());
			}

			BPUtil.SimpleHttp.SimpleHttpLogger.RegisterLogger(Logger.httpLogger, s.verboseWebServerLogs);

			SetTimeout.OnBackground(() =>
			{
				WebProxyService.UpdateWebServerBindings();
			}, 1000);

			return Get(true);
		}
		public ActionResult ForceRenew()
		{
			ForceRenewRequest request = ParseRequest<ForceRenewRequest>();

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
						string result = CertMgr.ForceRenew(entrypoint, exitpoint).Result;
						if (string.IsNullOrEmpty(result))
							return ApiSuccessNoAutocomplete(new ApiResponseBase(true));
						return ApiError(result);
					}
				}
			}
			return ApiError("Unable to find an acceptable Entrypoint that is routed to the chosen Exitpoint.");
		}
		public ActionResult GetRaw()
		{
			return PlainText(JsonConvert.SerializeObject(WebProxyService.MakeLocalSettingsReference(), Formatting.Indented));
		}
		public ActionResult UploadCertificate()
		{
			UploadCertificateRequest request = ParseRequest<UploadCertificateRequest>();

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
				WebProxyService.SaveNewSettings(newSettings);
			}

			DirectoryInfo diCertDir = new FileInfo(certPath).Directory;
			if (!diCertDir.Exists)
			{
				Robust.RetryPeriodic(() =>
				{
					Directory.CreateDirectory(diCertDir.FullName);
				}, 50, 6);
			}

			Robust.RetryPeriodic(() =>
			{
				File.WriteAllBytes(certPath, certBytes);
			}, 50, 6);

			return Get();
		}
		public ActionResult TestCloudflareDNS()
		{
			BasicEventTimer bet = new BasicEventTimer();
			bet.Start("Get domain name");
			try
			{
				string anyDomainName = CloudflareDnsValidator.GetAnyConfiguredDomain().Result;
				if (string.IsNullOrWhiteSpace(anyDomainName))
					return ApiError("Test failed: Found null or whitespace domain name in Cloudflare account.");

				bet.Start("Create DNS record");
				string key = "_test-access-token." + anyDomainName;
				string value = TimeUtil.GetTimeInMsSinceEpoch().ToString();

				Task t = CloudflareDnsValidator.CreateDNSRecord(key, value);
				t.Wait();

				bet.Start("Delete DNS record");
				int deleted = CloudflareDnsValidator.DeleteDNSRecord(key).Result;

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
					Context.ResponseHeaders.Add("Content-Disposition", "attachment; filename=\"WebProxy_Export_" + StringUtil.MakeSafeForFileName(Context.httpProcessor.hostName) + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".zip\"");
				}
				return new FileDownloadResult(ms.ToArray(), false);
			}
		}
		public ActionResult Import()
		{
			UploadSettingsAndCertificatesRequest request = ParseRequest<UploadSettingsAndCertificatesRequest>();

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
					Robust.RetryPeriodic(() =>
					{
						Directory.CreateDirectory(fi.Directory.FullName);
						File.WriteAllBytes(path, cert.Data);
						File.SetLastWriteTime(path, cert.LastWriteTime);
					}, 50, 10);
				}
			}
			// Write Cert Renewal Dates
			if (certRenewalData != null)
			{
				Robust.RetryPeriodic(() =>
				{
					File.WriteAllBytes("CertRenewalDates.json", certRenewalData.Data);
				}, 50, 10);
			}
			// Write Settings
			return Set(settings);
		}
	}
	public class GetConfigurationResponse : ApiResponseBase
	{
		public string appVersion = Globals.AssemblyVersion;
		public string acmeAccountEmail;
		public string errorTrackerSubmitUrl;
		public string cloudflareApiToken;
		public bool verboseWebServerLogs;
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
}

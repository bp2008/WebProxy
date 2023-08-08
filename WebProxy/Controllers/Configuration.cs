using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
			Settings s = WebProxyService.MakeLocalSettingsReference();
			GetConfigurationResponse response = new GetConfigurationResponse();
			response.acmeAccountEmail = s.acmeAccountEmail;
			response.entrypoints = s.entrypoints.ToArray();
			response.exitpoints = s.exitpoints.ToArray();
			response.middlewares = s.middlewares.ToArray();
			response.proxyRoutes = s.proxyRoutes.ToArray();
			response.errorTrackerSubmitUrl = s.errorTrackerSubmitUrl;
			response.cloudflareApiToken = s.cloudflareApiToken;
			return Json(response);
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
			try
			{
				WebProxyService.SaveNewSettings(s);
			}
			catch (Exception ex)
			{
				return ApiError(ex.FlattenMessages());
			}

			WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);

			SetTimeout.OnBackground(() =>
			{
				WebProxyService.UpdateWebServerBindings();
			}, 1000);

			s = WebProxyService.MakeLocalSettingsReference();
			GetConfigurationResponse response = new GetConfigurationResponse();
			response.acmeAccountEmail = s.acmeAccountEmail;
			response.entrypoints = s.entrypoints.ToArray();
			response.exitpoints = s.exitpoints.ToArray();
			response.middlewares = s.middlewares.ToArray();
			response.proxyRoutes = s.proxyRoutes.ToArray();
			response.errorTrackerSubmitUrl = s.errorTrackerSubmitUrl;
			response.cloudflareApiToken = s.cloudflareApiToken;

			{
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

			return Json(response);
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
							return Json(new ApiResponseBase(true));
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
					return Json(new ApiResponseBase(true));
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
	}
	public class GetConfigurationResponse : ApiResponseBase
	{
		public string appVersion = Globals.AssemblyVersion;
		public string acmeAccountEmail;
		public string errorTrackerSubmitUrl;
		public string cloudflareApiToken;
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
}

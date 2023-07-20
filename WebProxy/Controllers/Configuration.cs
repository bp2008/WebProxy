using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy.Controllers
{
	public class Configuration : AdminConsoleControllerBase
	{
		public ActionResult Get()
		{
			Settings s = WebProxyService.MakeLocalSettingsReference();
			GetConfigurationResponse response = new GetConfigurationResponse();
			response.entrypoints = s.entrypoints.ToArray();
			response.exitpoints = s.exitpoints.ToArray();
			response.middlewares = s.middlewares.ToArray();
			response.proxyRoutes = s.proxyRoutes.ToArray();
			return Json(response);
		}
		public ActionResult Set()
		{
			SetConfigurationRequest request = ParseRequest<SetConfigurationRequest>();

			Settings s = WebProxyService.CloneSettingsObjectSlow();
			s.entrypoints = request.entrypoints.ToList();
			s.exitpoints = request.exitpoints.ToList();
			s.middlewares = request.middlewares.ToList();
			s.proxyRoutes = request.proxyRoutes.ToList();
			WebProxyService.SaveNewSettings(s);

			WebProxyService.SettingsValidateAndAdminConsoleSetup(out Entrypoint adminEntry, out Exitpoint adminExit, out Middleware adminLogin);

			SetTimeout.OnBackground(() =>
			{
				WebProxyService.UpdateWebServerBindings();
			}, 1000);

			s = WebProxyService.MakeLocalSettingsReference();
			GetConfigurationResponse response = new GetConfigurationResponse();
			response.entrypoints = s.entrypoints.ToArray();
			response.exitpoints = s.exitpoints.ToArray();
			response.middlewares = s.middlewares.ToArray();
			response.proxyRoutes = s.proxyRoutes.ToArray();

			{
				string adminHost = adminExit.host;
				adminHost = adminHost?.Replace("*", "");
				if (string.IsNullOrEmpty(adminHost))
					adminHost = "localhost";

				List<string> adminEntryOrigins = new List<string>();
				if (adminEntry.httpPortValid())
					adminEntryOrigins.Add("http://" + adminHost + (adminEntry.httpPort == 80 ? "" : (":" + adminEntry.httpPort)));
				if (adminEntry.httpsPortValid())
					adminEntryOrigins.Add("https://" + adminHost + (adminEntry.httpsPort == 443 ? "" : (":" + adminEntry.httpsPort)));

				response.adminEntryOrigins = adminEntryOrigins.ToArray();
			}

			return Json(response);
		}
	}
	public class GetConfigurationResponse : ApiResponseBase
	{
		public Entrypoint[] entrypoints;
		public Exitpoint[] exitpoints;
		public Middleware[] middlewares;
		public ProxyRoute[] proxyRoutes;
		public string[] exitpointTypes = Enum.GetNames(typeof(ExitpointType));
		public string[] middlewareTypes = Enum.GetNames(typeof(MiddlewareType));

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string[] adminEntryOrigins;
		public GetConfigurationResponse() : base(true, null)
		{
		}
	}
	public class SetConfigurationRequest
	{
		public Entrypoint[] entrypoints;
		public Exitpoint[] exitpoints;
		public Middleware[] middlewares;
		public ProxyRoute[] proxyRoutes;
	}
}

using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WebProxy.Controllers;
using WebProxy.LetsEncrypt;

namespace WebProxy
{
	public class WebServer : HttpServer
	{
		MVCMain mvcAdminConsole;
		ViteProxy viteProxy = null;
		public WebServer() : base(CreateCertificateSelector())
		{
			SimpleHttpLogger.RegisterLogger(Logger.httpLogger, true);
			MvcJson.DeserializeObject = JsonConvert.DeserializeObject;
			MvcJson.SerializeObject = JsonConvert.SerializeObject;
			mvcAdminConsole = new MVCMain(Assembly.GetExecutingAssembly(), typeof(AdminConsoleControllerBase).Namespace, (Context, ex) => Logger.Debug(ex, "AdminConsole: " + Context.OriginalRequestPath));
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached)
				viteProxy = new ViteProxy(5173, Globals.ApplicationDirectoryBase + "../../WebProxy-Admin");
#endif
		}

		private static ICertificateSelector CreateCertificateSelector()
		{
			return new WebProxyCertificateSelector();
		}

		public override void handleGETRequest(HttpProcessor p)
		{
			handleAllRequests(p);
		}

		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			handleAllRequests(p);
		}
		/// <summary>
		/// Handles requests using less common Http verbs such as "HEAD" or "PUT". See <see cref="HttpMethods"/>.
		/// </summary>
		/// <param name="method">The HTTP method string, e.g. "HEAD" or "PUT". See <see cref="HttpMethods"/>.</param>
		/// <param name="p">The HttpProcessor handling the request.</param>
		public override void handleOtherRequest(HttpProcessor p, string method)
		{
			handleAllRequests(p);
		}
		private void handleAllRequests(HttpProcessor p)
		{
			Settings settings = WebProxyService.MakeLocalSettingsReference();
			BasicEventTimer bet = new BasicEventTimer("0.000");
			try
			{
				// Identify the entrypoint and exitpoint which this request is targeting.
				bet.Start("Request Routing");
				Entrypoint[] matchedEntrypoints = settings.identifyThisEntrypoint((IPEndPoint)p.tcpClient.Client.RemoteEndPoint, (IPEndPoint)p.tcpClient.Client.LocalEndPoint, p.secure_https);
				if (matchedEntrypoints.Length == 0)
				{
					Logger.Info("Unable to identify any matching entrypoint for request from client " + p.RemoteIPAddressStr + " to " + p.request_url);
					p.writeFailure("500 Internal Server Error");
					return;
				}

				Exitpoint myExitpoint = settings.identifyThisExitpoint(matchedEntrypoints, p, out Entrypoint myEntrypoint);
				if (myExitpoint == null || myExitpoint.type == ExitpointType.Disabled)
				{
					// Set responseWritten = true to prevent a fallback response.  We want this connection to simply close.
					Logger.Info("No exitpoint for request from client " + p.RemoteIPAddressStr + " to " + p.request_url);
					p.responseWritten = true;
					return;
				}

				// ACME Validation: HTTP-01
				if (myExitpoint.autoCertificate && p.requestedPage.StartsWith(".well-known/acme-challenge/"))
				{
					// We could restrict this to only unsecured requests on port 80, but for debugging purposes it 
					//   can be useful to allow the request on any port and protocol that reaches this exitpoint.
					string fileName = p.requestedPage.Substring(".well-known/acme-challenge/".Length);
					string payload = CertMgr.GetHttpChallengeResponse(p.hostName, fileName, myEntrypoint, myExitpoint);
					Logger.Info("ACME HTTP-01: " + p.RemoteIPAddressStr + " -> " + p.hostName + ": " + p.request_url.ToString() + " -> " + payload);
					if (payload == null)
						p.writeFailure("404 Not Found");
					else
						p.writeFullResponseUTF8(payload, "text/plain; charset=utf-8");
					return;
				}

				// Apply Middlewares in a specific order based on their type.
				IEnumerable<Middleware> allApplicableMiddlewares = settings.middlewares
					.Where(m => myEntrypoint.middlewares?.Contains(m.Id) == true
								|| myExitpoint.middlewares?.Contains(m.Id) == true);

				// MiddlewareType.RedirectHttpToHttps
				if (!p.secure_https && myEntrypoint.httpsPortValid())
				{
					if (allApplicableMiddlewares.Any(m => m.Type == MiddlewareType.RedirectHttpToHttps))
					{
						UriBuilder builder = new UriBuilder(p.request_url);
						builder.Port = myEntrypoint.httpsPort;
						p.writeRedirect(builder.Uri.ToString());
						return;
					}
				}

				// MiddlewareType.IPWhitelist
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.IPWhitelist))
				{
					Logger.Info("Middleware \"" + m.Id + "\" is unable to execute because type MiddlewareType.IPWhitelist is not implemented in this version of WebProxy.");
				}

				// MiddlewareType.HttpDigestAuth
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.HttpDigestAuth))
				{
					string realm = p.hostName == null ? "WebProxy" : p.hostName;
					IEnumerable<NetworkCredential> credentials = m.AuthCredentials.Select(a => new NetworkCredential(a.User, a.Pass));
					NetworkCredential userCredential = p.ValidateDigestAuth(realm, credentials);
					if (userCredential == null)
					{
						List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();
						headers.Add(new KeyValuePair<string, string>("WWW-Authenticate", p.GetDigestAuthWWWAuthenticateHeaderValue(realm)));
						p.writeFailure("401 Unauthorized", additionalHeaders: headers);
						return;
					}
				}

				// Process the request in the context of the chosen Exitpoint.
				if (myExitpoint.type == ExitpointType.AdminConsole)
				{
					if (!mvcAdminConsole.ProcessRequest(p))
					{
						if (viteProxy != null)
						{
							bet.Start("Proxy AdminConsole request: " + p.http_method + " " + p.request_url);
							// Handle hot module reload provided by Vite dev server.
							viteProxy.Proxy(p);
							bet.Stop();
							return;
						}
						else
						{
							bet.Start("Handle AdminConsole request: " + p.http_method + " " + p.request_url);
							#region www
							string wwwDirectoryBase = Globals.ApplicationDirectoryBase + "www" + '/';

							FileInfo fi = new FileInfo(wwwDirectoryBase + p.requestedPage);
							string targetFilePath = fi.FullName.Replace('\\', '/');
							if (!targetFilePath.StartsWith(wwwDirectoryBase) || targetFilePath.Contains("../"))
							{
								p.writeFailure("400 Bad Request");
								return;
							}
							if (p.requestedPage.IEquals(""))
								fi = new FileInfo(wwwDirectoryBase + "index.html");
							if (!fi.Exists)
							{
								p.writeFailure("404 Not Found");
								return;
							}
							if (fi.LastWriteTimeUtc.ToString("R") == p.GetHeaderValue("if-modified-since"))
							{
								p.writeSuccess(Mime.GetMimeType(fi.Extension), -1, "304 Not Modified");
								return;
							}
							using (FileStream fs = fi.OpenRead())
							{
								p.writeSuccess(Mime.GetMimeType(fi.Extension), fi.Length, additionalHeaders: GetCacheLastModifiedHeaders(TimeSpan.FromHours(1), fi.LastWriteTimeUtc));
								p.outputStream.Flush();
								fs.CopyTo(p.tcpStream);
								p.tcpStream.Flush();
							}
							#endregion
							bet.Stop();
						}
					}
				}
				else if (myExitpoint.type == ExitpointType.WebProxy)
				{
					bet.Start("Start proxying " + p.http_method + " " + p.request_url);
					Uri destinationOrigin = new Uri(myExitpoint.destinationOrigin);
					UriBuilder builder = new UriBuilder(p.request_url);
					builder.Scheme = destinationOrigin.Scheme;
					builder.Host = destinationOrigin.DnsSafeHost;
					builder.Port = destinationOrigin.Port;

					HttpProcessor.ProxyOptions options = new HttpProcessor.ProxyOptions();
					options.networkTimeoutMs = 15000;
					options.snoopy = new ProxyDataBuffer();
					options.bet = bet;
					options.host = myExitpoint.destinationHostHeader;

					bet.Start("Calling p.ProxyToAsync");
					p.ProxyToAsync(builder.Uri.ToString(), options).Wait();
					bet.Stop();
				}
				else
				{
					Logger.Info("Unhandled Exitpoint type `" + myExitpoint.type + "` in " + JsonConvert.SerializeObject(myExitpoint));
				}
			}
			finally
			{
				bet.Stop();
				//Logger.Info(p.http_method + " " + p.request_url + "\r\n\r\n" + bet.ToString("\r\n") + "\r\n");
			}
		}
		private List<KeyValuePair<string, string>> GetCacheLastModifiedHeaders(TimeSpan maxAge, DateTime lastModifiedUTC)
		{
			List<KeyValuePair<string, string>> additionalHeaders = new List<KeyValuePair<string, string>>();
			additionalHeaders.Add(new KeyValuePair<string, string>("Cache-Control", "max-age=" + (long)maxAge.TotalSeconds + ", public"));
			additionalHeaders.Add(new KeyValuePair<string, string>("Last-Modified", lastModifiedUTC.ToString("R")));
			return additionalHeaders;
		}

		protected override void stopServer()
		{
		}

		/// <summary>
		/// Reconfigures the web server to listen on all entrypoints currently in the settings object.
		/// </summary>
		internal void UpdateBindings()
		{
			Settings settings = WebProxyService.MakeLocalSettingsReference();

			// These collections will contain all IP endpoints that are configured to provide HTTP or HTTPS.
			HashSet<IPEndPoint> httpBindings = new HashSet<IPEndPoint>();
			HashSet<IPEndPoint> httpsBindings = new HashSet<IPEndPoint>();
			foreach (Entrypoint entrypoint in settings.entrypoints)
			{
				AddBinding(httpBindings, entrypoint.httpPort, entrypoint.ipAddress);
				AddBinding(httpsBindings, entrypoint.httpsPort, entrypoint.ipAddress);
			}

			// Convert the endpoint HashSets to a list of Bindings.
			List<Binding> bindings = new List<Binding>();
			foreach (IPEndPoint ipep in httpBindings)
			{
				if (httpsBindings.Contains(ipep))
					bindings.Add(new Binding(AllowedConnectionTypes.httpAndHttps, ipep));
				else
					bindings.Add(new Binding(AllowedConnectionTypes.http, ipep));
			}
			foreach (IPEndPoint ipep in httpsBindings)
			{
				if (!httpBindings.Contains(ipep))
					bindings.Add(new Binding(AllowedConnectionTypes.https, ipep));
			}
			this.SetBindings(bindings.ToArray());
		}

		private void AddBinding(HashSet<IPEndPoint> bindings, int port, string ipAddress)
		{
			if (port < 1 || port > 65535)
				return;
			if (IPAddress.TryParse(ipAddress, out IPAddress ip))
				bindings.Add(new IPEndPoint(ip, port));
			else
			{
				bindings.Add(new IPEndPoint(IPAddress.Any, port));
				bindings.Add(new IPEndPoint(IPAddress.IPv6Any, port));
			}
		}
	}
}

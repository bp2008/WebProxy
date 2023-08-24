using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using BPUtil.SimpleHttp.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
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
		public static string projectDirPath { get; private set; } = "";

		public WebServer() : base(CreateCertificateSelector())
		{
			MVCGlobals.RemoteClientsMaySeeExceptionDetails = true;
			MvcJson.DeserializeObject = JsonConvert.DeserializeObject;
			MvcJson.SerializeObject = JsonConvert.SerializeObject;
			mvcAdminConsole = new MVCMain(Assembly.GetExecutingAssembly(), typeof(AdminConsoleControllerBase).Namespace, MvcErrorHandler);
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached)
			{
				string binFolderPath = FileUtil.FindAncestorDirectory(Globals.ApplicationDirectoryBase, "bin");
				if (binFolderPath == null)
					throw new ApplicationException("Unable to locate bin folder in path: " + Globals.ApplicationDirectoryBase);
				projectDirPath = new DirectoryInfo(binFolderPath).Parent.FullName;
				string path = Path.Combine(projectDirPath, "WebProxy-Admin");
				viteProxy = new ViteProxy(5173, path);
			}
#endif
		}

		private static void MvcErrorHandler(RequestContext Context, Exception ex)
		{
			if (!HttpProcessor.IsOrdinaryDisconnectException(ex))
				WebProxyService.ReportError(ex, "AdminConsole: " + Context.OriginalRequestPath);
		}

		private static ICertificateSelector CreateCertificateSelector()
		{
			return new WebProxyCertificateSelector();
		}

		public override void handleGETRequest(HttpProcessor p)
		{
			handleAllRequests(p);
		}

		public override void handlePOSTRequest(HttpProcessor p)
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
					// This situation is normal if connections are already open to an Entrypoint that is going away.
					Logger.Info("Unable to identify any matching entrypoint for request from client " + p.RemoteIPAddressStr + " to " + p.request_url);
					p.PreventKeepalive();
					p.writeFailure("500 Internal Server Error");
					return;
				}

				Exitpoint myExitpoint = settings.identifyThisExitpoint(matchedEntrypoints, p, out Entrypoint myEntrypoint);
				if (myExitpoint == null || myExitpoint.type == ExitpointType.Disabled)
				{
					// Set responseWritten = true to prevent a fallback response.  We want this connection to simply close.
					//Logger.Info("No exitpoint for request from client " + p.RemoteIPAddressStr + " to " + p.request_url);
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
						builder.Host = p.hostName;
						builder.Scheme = "https";
						p.writeRedirect(builder.Uri.ToString());
						return;
					}
				}

				// MiddlewareType.IPWhitelist
				if (!IPWhitelistCheck(p.TrueRemoteIPAddress, allApplicableMiddlewares))
				{
					// IP whitelisting is in effect, but the client is not communicating from a whitelisted IP.  Close the connection without writing a response.
					p.responseWritten = true;
					return;
				}

				// MiddlewareType.HttpDigestAuth
				{
					NetworkCredential userCredential = null;
					bool authRequired = false;
					string realm = p.hostName == null ? "WebProxy" : p.hostName;
					foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.HttpDigestAuth))
					{
						authRequired = true;
						IEnumerable<NetworkCredential> credentials = m.AuthCredentials.Select(a => new NetworkCredential(a.User, a.Pass));
						userCredential = p.ValidateDigestAuth(realm, credentials);
						if (userCredential != null)
							break;
					}
					if (authRequired && userCredential == null)
					{
						HttpHeaderCollection headers = new HttpHeaderCollection();
						headers.Add("WWW-Authenticate", p.GetDigestAuthWWWAuthenticateHeaderValue(realm));
						p.writeFailure("401 Unauthorized", additionalHeaders: headers);
						return;
					}
				}

				// MiddlewareType.AddProxyServerTiming
				bool AddProxyServerTiming = allApplicableMiddlewares.Any(m => m.Type == MiddlewareType.AddProxyServerTiming);

				// MiddlewareType.AddHttpHeaderToResponse
				HttpHeaderCollection overrideResponseHeaders = new HttpHeaderCollection();
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.AddHttpHeaderToResponse))
				{
					foreach (string header in m.HttpHeaders)
					{
						if (string.IsNullOrWhiteSpace(header))
							continue;

						int separator = header.IndexOf(':');
						if (separator == -1)
							throw new ApplicationException("Invalid http header line in middleware \"" + m.Id + "\": " + header);

						string name = header.Substring(0, separator);
						int pos = separator + 1;
						while (pos < header.Length && header[pos] == ' ')
							pos++; // strip any spaces

						string value = header.Substring(pos);
						overrideResponseHeaders[name] = value;
					}
				}

				// Proxy Header Middlewares
				Middleware xff = allApplicableMiddlewares.FirstOrDefault(m => m.Type == MiddlewareType.XForwardedFor);
				Middleware xfh = allApplicableMiddlewares.FirstOrDefault(m => m.Type == MiddlewareType.XForwardedHost);
				Middleware xfp = allApplicableMiddlewares.FirstOrDefault(m => m.Type == MiddlewareType.XForwardedProto);
				Middleware xri = allApplicableMiddlewares.FirstOrDefault(m => m.Type == MiddlewareType.XRealIp);

				List<string> trustedProxyIPRanges = new List<string>();
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.TrustedProxyIPRanges))
				{
					if (m.WhitelistedIpRanges != null)
						trustedProxyIPRanges.AddRange(m.WhitelistedIpRanges);
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
							p.writeStaticFile(fi);
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

					ProxyOptions options = new ProxyOptions();
					options.networkTimeoutMs = 15000;
					options.bet = bet;
					options.host = myExitpoint.destinationHostHeader;
					options.includeServerTimingHeader = AddProxyServerTiming;
					options.xForwardedFor = xff?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.xForwardedHost = xfh?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.xForwardedProto = xfp?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.xRealIp = xri?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.proxyHeaderTrustedIpRanges = trustedProxyIPRanges.ToArray();
					options.allowConnectionKeepalive = myExitpoint.useConnectionKeepAlive;

					HttpHeader[] orh = overrideResponseHeaders.GetHeaderArray();
					if (orh.Length > 0)
					{
						options.BeforeResponseHeadersSent += (sender, e) =>
						{
							foreach (HttpHeader header in orh)
								e[header.Key] = header.Value;
						};
					}

					bet.Start("Calling p.ProxyToAsync");
					try
					{
						p.ProxyToAsync(builder.Uri.ToString(), options).Wait();
						bet.Stop();
						if (settings.verboseWebServerLogs)
							Logger.Info("Proxy Completed: " + p.http_method + " " + p.request_url + "\r\n\r\n" + options.log.ToString() + "\r\n");
					}
					catch (Exception ex)
					{
						bet.Stop();
						if (settings.verboseWebServerLogs)
							Logger.Info("Proxy Completed With Error: " + p.http_method + " " + p.request_url + "\r\n\r\n" + options.log.ToString() + "\r\n");
						ex.Rethrow();
					}
				}
				else
				{
					WebProxyService.ReportError("Unhandled Exitpoint type `" + myExitpoint.type + "` in " + JsonConvert.SerializeObject(myExitpoint));
				}
			}
			finally
			{
				bet.Stop();
				//Logger.Info(p.http_method + " " + p.request_url + "\r\n\r\n" + bet.ToString("\r\n") + "\r\n");
			}
		}
		/// <inheritdoc/>
		protected override void stopServer()
		{
		}

		/// <summary>
		/// If this method returns true, socket bind events will be logged normally.  If false, they will use the LogVerbose call.
		/// </summary>
		/// <returns></returns>
		public override bool shouldLogSocketBind()
		{
			return true;
		}

		/// <summary>
		/// If this method returns true, requests should be logged to a file.
		/// </summary>
		/// <returns></returns>
		public override bool shouldLogRequestsToFile()
		{
			return WebProxyService.MakeLocalSettingsReference().verboseWebServerLogs;
		}

		private object updateBindingsLock = new object();
		/// <summary>
		/// Reconfigures the web server to listen on all entrypoints currently in the settings object.
		/// </summary>
		internal void UpdateBindings()
		{
			lock (updateBindingsLock)
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
		/// <summary>
		/// Given an IP address and a collection of IPWhitelist middlewares, returns true if the given IP address is allowed to access the resource.  The IP is allowed if there are no IPWhitelist middlewares or if the IP is on one of the whitelists.
		/// </summary>
		/// <param name="remoteIPAddress">IP address that must be authenticated against a possible collection of IPWhitelist middlewares.</param>
		/// <param name="middlewares">Collection of middlewares. If this contains no IPWhitelist middlewares, the method will simply return true.</param>
		/// <returns></returns>
		public static bool IPWhitelistCheck(IPAddress remoteIPAddress, IEnumerable<Middleware> middlewares)
		{
			bool ipNeedsWhitelisted = false;
			foreach (Middleware m in middlewares.Where(m => m.Type == MiddlewareType.IPWhitelist))
			{
				ipNeedsWhitelisted = true;
				if (IPAddressRange.WhitelistCheck(remoteIPAddress, m.WhitelistedIpRanges))
					return true;
			}
			return !ipNeedsWhitelisted;
		}

		/// <summary>
		/// Gets a collection of allowed TLS cipher suites for the given connection.  Returns null if the default set of cipher suites should be allowed (which varies by platform).
		/// </summary>
		/// <param name="p">HttpProcessor providing connection information so that the derived class can decide which cipher suites to allow.</param>
		/// <returns>A collection of allowed TLS cipher suites, or null.</returns>
		public override IEnumerable<TlsCipherSuite> GetAllowedCipherSuites(HttpProcessor p)
		{
			Settings settings = WebProxyService.MakeLocalSettingsReference();
			Entrypoint[] matchedEntrypoints = settings.identifyThisEntrypoint((IPEndPoint)p.tcpClient.Client.RemoteEndPoint, (IPEndPoint)p.tcpClient.Client.LocalEndPoint, true);
			if (matchedEntrypoints.Length == 0)
				return null;

			Exitpoint myExitpoint = settings.identifyThisExitpoint(matchedEntrypoints, p, out Entrypoint myEntrypoint);
			if (myExitpoint == null || myExitpoint.type == ExitpointType.Disabled)
				return null;

			if (myEntrypoint.tlsCipherSuiteSet == TlsCipherSuiteSet.DotNet5_Q3_2023)
				return new BPUtil.SimpleHttp.TLS.TlsCipherSuiteSet_DotNet5_Q3_2023().GetCipherSuites();
			else if (myEntrypoint.tlsCipherSuiteSet == TlsCipherSuiteSet.IANA_Q3_2023)
				return new BPUtil.SimpleHttp.TLS.TlsCipherSuiteSet_IANA_Q3_2023().GetCipherSuites();
			else
				return null;
		}
	}
}

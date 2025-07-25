﻿using BPUtil;
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
using System.Threading;
using System.Threading.Tasks;
using WebProxy.Controllers;
using WebProxy.LetsEncrypt;

namespace WebProxy
{
	public class WebServer : HttpServerAsync
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

		public override async Task handleRequest(HttpProcessor p, string method, CancellationToken cancellationToken = default)
		{
			Settings settings = WebProxyService.MakeLocalSettingsReference();
			BasicEventTimer bet = new BasicEventTimer("0.000");
			try
			{
				// Identify the entrypoint and exitpoint which this request is targeting.
				bet.Start("Request Routing");
				Entrypoint[] matchedEntrypoints = settings.identifyThisEntrypoint(p.RemoteEndPoint, p.LocalEndPoint, p.secure_https);
				if (matchedEntrypoints.Length == 0)
				{
					// This situation is normal if connections are already open to an Entrypoint that is going away.
					Logger.Info("Unable to identify any matching entrypoint for request from client " + p.RemoteIPAddressStr + " to " + p.Request.Url);
					p.Response.PreventKeepalive();
					p.Response.Simple("500 Internal Server Error");
					return;
				}

				Exitpoint myExitpoint = settings.identifyThisExitpoint(matchedEntrypoints, p, out Entrypoint myEntrypoint);
				if (myExitpoint == null || myExitpoint.type == ExitpointType.Disabled)
				{
					// Prevent a fallback response.  We want this connection to simply close.
					//Logger.Info("No exitpoint for request from client " + p.RemoteIPAddressStr + " to " + p.Request.Url);
					p.Response.CloseWithoutResponse();
					return;
				}

				// ACME Validation: HTTP-01
				if (myExitpoint.autoCertificate && p.Request.Page.StartsWith(".well-known/acme-challenge/"))
				{
					// We could restrict this to only unsecured requests on port 80, but for debugging purposes it 
					//   can be useful to allow the request on any port and protocol that reaches this exitpoint.
					string fileName = p.Request.Page.Substring(".well-known/acme-challenge/".Length);
					string payload = CertMgr.GetHttpChallengeResponse(p.HostName, fileName, myEntrypoint, myExitpoint);
					Logger.Info("ACME HTTP-01: " + p.RemoteIPAddressStr + " -> " + p.HostName + ": " + p.Request.Url.ToString() + " -> " + payload);
					if (payload == null)
						p.Response.Simple("404 Not Found");
					else
						p.Response.FullResponseUTF8(payload, "text/plain; charset=utf-8");
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
						UriBuilder builder = new UriBuilder(p.Request.Url);
						builder.Port = myEntrypoint.httpsPort;
						builder.Host = p.HostName;
						builder.Scheme = "https";
						await p.Response.RedirectTemporaryAsync(builder.Uri.ToString(), cancellationToken).ConfigureAwait(false);
						return;
					}
				}

				// MiddlewareType.IPWhitelist
				if (!IPWhitelistCheck(p.TrueRemoteIPAddress, allApplicableMiddlewares))
				{
					// IP whitelisting is in effect, but the client is not communicating from a whitelisted IP.  Close the connection without writing a response.
					p.Response.CloseWithoutResponse();
					return;
				}

				// MiddlewareType.HttpDigestAuth
				{
					NetworkCredential userCredential = null;
					bool authRequired = false;
					string realm = p.HostName == null ? "WebProxy" : p.HostName;
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
						p.Response.Simple("401 Unauthorized");
						p.Response.Headers.Set("WWW-Authenticate", p.GetDigestAuthWWWAuthenticateHeaderValue(realm));
						return;
					}
				}

				// MiddlewareType.AddProxyServerTiming
				bool AddProxyServerTiming = allApplicableMiddlewares.Any(m => m.Type == MiddlewareType.AddProxyServerTiming);

				// MiddlewareType.AddHttpHeaderToRequest
				List<string> overrideRequestHeaders = new List<string>();
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.AddHttpHeaderToRequest))
					overrideRequestHeaders.AddRange(m.HttpHeaders);

				// MiddlewareType.AddHttpHeaderToResponse
				List<string> overrideResponseHeaders = new List<string>();
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.AddHttpHeaderToResponse))
					overrideResponseHeaders.AddRange(m.HttpHeaders);

				// MiddlewareType.HostnameSubstitution
				List<KeyValuePair<string, string>> hostnameSubstitutions = new List<KeyValuePair<string, string>>();
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.HostnameSubstitution))
						hostnameSubstitutions.AddRange(m.HostnameSubstitutions);

				// MiddlewareType.RegexReplaceInResponse
				List<KeyValuePair<string, string>> regexReplacements = new List<KeyValuePair<string, string>>();
				foreach (Middleware m in allApplicableMiddlewares.Where(m => m.Type == MiddlewareType.RegexReplaceInResponse))
					regexReplacements.AddRange(m.RegexReplacements);

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
					if (!await mvcAdminConsole.ProcessRequestAsync(p, cancellationToken: cancellationToken).ConfigureAwait(false))
					{
						if (viteProxy != null)
						{
							bet.Start("Proxy AdminConsole request: " + p.Request.HttpMethod + " " + p.Request.Url);
							// Handle hot module reload provided by Vite dev server.
							await viteProxy.ProxyAsync(p, cancellationToken).ConfigureAwait(false);
							bet.Stop();
							return;
						}
						else
						{
							bet.Start("Handle AdminConsole request: " + p.Request.HttpMethod + " " + p.Request.Url);
							#region www
							string wwwDirectoryBase = Globals.ApplicationDirectoryBase + "www" + '/';

							FileInfo fi = new FileInfo(wwwDirectoryBase + p.Request.Page);
							string targetFilePath = fi.FullName.Replace('\\', '/');
							if (!targetFilePath.StartsWith(wwwDirectoryBase) || targetFilePath.Contains("../"))
							{
								p.Response.Simple("400 Bad Request");
								return;
							}
							if (p.Request.Page.IEquals(""))
								fi = new FileInfo(wwwDirectoryBase + "index.html");
							await p.Response.StaticFileAsync(fi, cancellationToken: cancellationToken).ConfigureAwait(false);
							#endregion
							bet.Stop();
						}
					}
				}
				else if (myExitpoint.type == ExitpointType.WebProxy)
				{
					bet.Start("Start proxying " + p.Request.HttpMethod + " " + p.Request.Url);
					Uri destinationOrigin = new Uri(myExitpoint.destinationOrigin);
					UriBuilder builder = new UriBuilder(p.Request.Url);
					builder.Scheme = destinationOrigin.Scheme;
					builder.Host = destinationOrigin.DnsSafeHost;
					builder.Port = destinationOrigin.Port;

					ProxyOptions options = new ProxyOptions();
					options.connectTimeoutMs = myExitpoint.connectTimeoutSec * 1000;
					options.networkTimeoutMs = myExitpoint.networkTimeoutSec * 1000;
					options.bet = bet;
					options.host = myExitpoint.destinationHostHeader;
					options.includeServerTimingHeader = AddProxyServerTiming;
					options.xForwardedFor = xff?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.xForwardedHost = xfh?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.xForwardedProto = xfp?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.xRealIp = xri?.ProxyHeaderBehavior ?? ProxyHeaderBehavior.Drop;
					options.proxyHeaderTrustedIpRanges = trustedProxyIPRanges.ToArray();
					options.allowConnectionKeepalive = myExitpoint.useConnectionKeepAlive;
					options.acceptAnyCert = myExitpoint.proxyAcceptAnyCertificate;
					options.cancellationToken = cancellationToken;
					options.responseHostnameSubstitutions = hostnameSubstitutions;
					options.responseRegexReplacements = regexReplacements;

					if (overrideRequestHeaders.Count > 0)
					{
						options.BeforeRequestHeadersSent += (sender, e) =>
						{
							foreach (string header in overrideRequestHeaders)
								OverrideHeader(p, e.Request.Headers, header);
						};
					}


					if (overrideResponseHeaders.Count > 0)
					{
						options.BeforeResponseHeadersSent += (sender, e) =>
						{
							foreach (string header in overrideResponseHeaders)
								OverrideHeader(p, e.Response.Headers, header);
						};
					}

					bet.Start("Calling p.ProxyToAsync");
					try
					{
						await p.ProxyToAsync(builder.Uri.ToString(), options).ConfigureAwait(false);
						bet.Stop();
						if (settings.verboseWebServerLogs)
							Logger.Info("Proxy Completed: " + p.Request.HttpMethod + " " + p.Request.Url + "\r\n\r\n" + options.log.ToString() + "\r\n");
					}
					catch (Exception ex)
					{
						bet.Stop();
						if (settings.verboseWebServerLogs)
							Logger.Info("Proxy Completed With Error: " + p.Request.HttpMethod + " " + p.Request.Url + "\r\n\r\n" + options.log.ToString() + "\r\n");
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
				//if (settings.verboseWebServerLogs)
				//	Logger.Info(p.Request.HttpMethod + " " + p.Request.Url + "\r\n\r\n" + bet.ToString("\r\n") + "\r\n");
			}
			return;
		}

		private void OverrideHeader(HttpProcessor p, HttpHeaderCollection headers, string header)
		{
			if (string.IsNullOrWhiteSpace(header))
				return;

			int separator = header.IndexOf(':');
			if (separator == -1)
				headers.Remove(header);
			else
			{
				string name = header.Substring(0, separator);
				int pos = separator + 1;
				while (pos < header.Length && header[pos] == ' ')
					pos++; // strip any spaces

				string value = header.Substring(pos);
				value = ReplaceHeaderMacros(p, value);
				headers.Set(name, value);
			}
		}

		private string ReplaceHeaderMacros(HttpProcessor p, string input)
		{
			StringBuilder result = new StringBuilder();
			int i = 0;
			while (i < input.Length)
			{
				if (input[i] == '$')
				{
					int start = i;
					i++;
					while (i < input.Length && StringUtil.IsAlphaNumericOrUnderscore(input[i]))
						i++;
					string macro = input.Substring(start, i - start);

					if (macro == "$remote_addr")
						result.Append(p.RemoteIPAddressStr);
					else if (macro == "$remote_port")
						result.Append(p.RemoteEndPoint.Port);
					else if (macro == "$request_proto")
						result.Append(p.secure_https ? "https" : "http");
					else if (macro == "$server_name")
						result.Append(p.HostName);
					else if (macro == "$server_port")
						result.Append(p.LocalEndPoint.Port);
					else
						result.Append(macro); // Undefined macros are output literally.
				}
				else
				{
					result.Append(input[i]);
					i++;
				}
			}
			return result.ToString();
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
			Entrypoint[] matchedEntrypoints = settings.identifyThisEntrypoint(p.RemoteEndPoint, p.LocalEndPoint, true);
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

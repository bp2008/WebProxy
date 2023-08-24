using BPUtil;
using BPUtil.SimpleHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	/// <summary>
	/// WebProxy's primary settings file.
	/// </summary>
	public class Settings : SerializableObjectJson
	{
		/// <summary>
		/// All entrypoints currently configured.
		/// </summary>
		public List<Entrypoint> entrypoints = new List<Entrypoint>();
		/// <summary>
		/// All exitpoints currently configured.
		/// </summary>
		public List<Exitpoint> exitpoints = new List<Exitpoint>();
		/// <summary>
		/// All middlewares currently configured.
		/// </summary>
		public List<Middleware> middlewares = new List<Middleware>();
		/// <summary>
		/// All proxy routes (mappings from Entrypoint to Exitpoint) currently configured.
		/// </summary>
		public List<ProxyRoute> proxyRoutes = new List<ProxyRoute>();
		/// <summary>
		/// Email address used for certificate creation with LetsEncrypt.  Changing this should nullify <see cref="acmeAccountKey"/>.
		/// </summary>
		public string acmeAccountEmail = "";
		/// <summary>
		/// Account key used for certificate creation with LetsEncrypt.  This should be nullified when <see cref="acmeAccountEmail"/> is changed.
		/// </summary>
		public string acmeAccountKey = null;
		/// <summary>
		/// If true at service startup, the web server will do verbose logging.
		/// </summary>
		public bool verboseWebServerLogs = false;
		/// <summary>
		/// If assigned, errors will be submitted to this ErrorTracker submission URL.
		/// </summary>
		public string errorTrackerSubmitUrl = null;
		/// <summary>
		/// If assigned, this is the Cloudflare API key which can be used for DNS configuration.
		/// </summary>
		public string cloudflareApiToken = null;
		/// <summary>
		/// [8-10000] The maximum number of connections this server will process simultaneously.
		/// </summary>
		public int serverMaxConnectionCount = 48;

		protected override object DeserializeFromJson(string str)
		{
			return Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(str);
		}

		protected override string SerializeToJson(object obj)
		{
			return Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
		}

		/// <summary>
		/// Returns all Entrypoints from the <see cref="entrypoints"/> collection which the client has connected to, in order of preference (Entrypoints listening on a single IP address appear earlier than Entrypoints listening on "all interfaces").
		/// </summary>
		/// <param name="remoteEndpoint">The remote endpoint of the connection.</param>
		/// <param name="localEndpoint">The local endpoint of the connection.</param>
		/// <param name="isHttps">True if the connection is using HTTPS.</param>
		/// <returns></returns>
		public Entrypoint[] identifyThisEntrypoint(IPEndPoint remoteEndpoint, IPEndPoint localEndpoint, bool isHttps)
		{
			List<Entrypoint> exactInterfaceMatches = new List<Entrypoint>();
			List<Entrypoint> allInterfaceMatches = new List<Entrypoint>();
			foreach (Entrypoint entrypoint in entrypoints)
			{
				if (!entrypoint.isIpMatch(localEndpoint.Address, out bool isExactMatch))
					continue;
				if (!isHttps && entrypoint.httpPort != localEndpoint.Port)
					continue;
				if (isHttps && entrypoint.httpsPort != localEndpoint.Port)
					continue;
				if (isExactMatch)
					exactInterfaceMatches.Add(entrypoint);
				else
					allInterfaceMatches.Add(entrypoint);
			}
			return exactInterfaceMatches.Concat(allInterfaceMatches).ToArray();
		}

		/// <summary>
		/// Returns the Exitpoint which is the best match for the current request, given an array of Entrypoints to consider.
		/// </summary>
		/// <param name="matchedEntrypoints">Entrypoints which were already matched to this request.</param>
		/// <param name="p">HttpProcessor instance handling a client connection.</param>
		/// <returns></returns>
		public Exitpoint identifyThisExitpoint(Entrypoint[] matchedEntrypoints, HttpProcessor p, out Entrypoint matchedEntrypoint)
		{
			foreach (Entrypoint entrypoint in matchedEntrypoints)
			{
				// Get every Exitpoint that is mapped to this Entrypoint.
				HashSet<string> exitpointNames = new HashSet<string>(proxyRoutes.Where(pr => pr.entrypointName == entrypoint.name).Select(pr => pr.exitpointName));
				IEnumerable<Exitpoint> mappedExitpoints = exitpoints.Where(e => exitpointNames.Contains(e.name));

				Exitpoint bestMatch = null;
				foreach (Exitpoint exit in mappedExitpoints)
				{
					if (exit.isHostnameMatch(p.hostName, out bool isExactMatch))
					{
						if (isExactMatch)
						{
							matchedEntrypoint = entrypoint;
							return exit;
						}
						if (bestMatch == null)
							bestMatch = exit;
					}
				}
				if (bestMatch != null)
				{
					matchedEntrypoint = entrypoint;
					return bestMatch;
				}
			}
			matchedEntrypoint = null;
			return null;
		}
	}
}
using BPUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebProxy
{
	/// <summary>
	/// Declares how requests to a hostname should be handled.
	/// </summary>
	public class Exitpoint
	{
		/// <summary>
		/// User-defined unique name for this exitpoint.
		/// </summary>
		public string name;
		/// <summary>
		/// <para>DNS hostname template.</para>
		/// <para>In order to access this Exitpoint, a client must request a host which matches this template.</para>
		/// <para>Null or empty string will make this Exitpoint be unreachable by a standard HTTP client, as a standard HTTP client would provide a non-empty Host header.</para>
		/// <para>Any number of '*' characters can be used as wildcards where each '*' means 0 or more characters.  Wildcard matches are lower priority than exact host matches.</para>
		/// </summary>
		public string host;
		/// <summary>
		/// If enabled, certificates for this host will be obtained and managed automatically via LetsEncrypt.  Automatic certificate management will only work if this exitpoint is mapped to an entrypoint that is reachable on the internet at 'http://host:80/' or 'https://host:443/'.  Wildcards are not allowed in <see cref="host"/> when using <see cref="autoCertificate"/>, unless you have enabled DNS validation.
		/// </summary>
		public bool autoCertificate;
		/// <summary>
		/// If enabled, and the certificate does not exist, a self-signed certificate will be created automatically.  This setting is inactive when using <see cref="autoCertificate"/>.
		/// </summary>
		public bool allowGenerateSelfSignedCertificate = true;
		/// <summary>
		/// [Requires autoCertificate == true] If enabled, DNS validation will prefer to use the DNS-01 method via your Cloudflare API Token.  All of the domains in [host] much be editable via your Cloudflare API Token or else validation will fail.
		/// </summary>
		public bool cloudflareDnsValidation;
		/// <summary>
		/// [Requires autoCertificate == false] Path to the certificate file (pfx). If omitted, a path will be automatically filled in upon first use.
		/// </summary>
		public string certificatePath;
		/// <summary>
		/// [Requires type == WebProxy] Requests shall be proxied to this origin, such as "https://example.com:8000".
		/// </summary>
		public string destinationOrigin;
		/// <summary>
		/// [Requires type == WebProxy] If you need to override the host string used in outgoing proxy requests (for the Host header and TLS Server Name Indication), provide the host string here.  Otherwise leave this null or empty and the host from [destinationOrigin] value will be used.
		/// </summary>
		public string destinationHostHeader;
		/// <summary>
		/// [Requires type == WebProxy] If true, skip certificate validation for destination origin.
		/// </summary>
		public bool proxyAcceptAnyCertificate = false;
		/// <summary>
		/// [Requires type == WebProxy] If true, regular requests sent to the destination server will use `Connection: keep-alive`, otherwise `Connection: close`.
		/// </summary>
		public bool useConnectionKeepAlive = true;
		/// <summary>
		/// <para>[Requires type == WebProxy]</para>
		/// <para>[Default: 10] The connection timeout, in milliseconds.</para>
		/// <para>Clamped to the range [1, 60].</para>
		/// <para>This timeout applies only to the Connect operation (when connecting to the destination server to faciliate proxying).</para>
		/// </summary>
		public int connectTimeoutSec = 10;
		/// <summary>
		/// <para>[Requires type == WebProxy]</para>
		/// <para>[Default: 15] The send and receive timeout for other time-sensitive network operations, in seconds.</para>
		/// <para>Clamped to the range [1, 600].</para>
		/// <para>This timeout applies to:</para>
		/// <para>* Reading the HTTP request body from the client.</para>
		/// <para>* Reading the HTTP response header from the destination server.</para>
		/// <para>* All other proxy operations that send data on a network socket.</para>
		/// <para>If a destination sometimes has slow time-to-first-byte, you may need to increase this timeout.</para>
		/// <para>This timeout does not apply when reading a response body or WebSocket data because these actions often sit idle for extended periods of time.</para>
		/// </summary>
		public int networkTimeoutSec = 15;
		/// <summary>
		/// Defines the type of Exitpoint this is.  Can be Disabled, AdminConsole, or WebProxy.
		/// </summary>
		[JsonConverter(typeof(StringEnumConverter))]
		public ExitpointType type = ExitpointType.Disabled;
		/// <summary>
		/// Array of unique identifiers for middlewares that apply to all requests matching this Exitpoint.
		/// </summary>
		public string[] middlewares;
		/// <summary>
		/// Returns true if the given hostname matches the [host] configured here.
		/// </summary>
		/// <param name="hostFromRequest">Hostname requested by the client.</param>
		/// <param name="isExactMatch">This is set = true only if the host template is a match for the given hostname without using any wildcards.</param>
		/// <returns></returns>
		public bool isHostnameMatch(string hostFromRequest, out bool isExactMatch)
		{
			string[] allHosts = getAllDomains();
			foreach (string h in allHosts)
			{
				if (hostCompare(hostFromRequest, h, out isExactMatch))
					return true;
			}
			isExactMatch = false;
			return false;
		}
		/// <summary>
		/// Returns the host string that matches the host string from the client, or null if none match.  If there is no exact match, this method does wildcard expansion and may return a hostname with a wildcard in it (e.g. "*.example.com").
		/// </summary>
		/// <param name="hostFromRequest">Hostname requested by the client.</param>
		/// <returns>The host string that matches the host string from the client, or null if none match</returns>
		public string getHostnameMatch(string hostFromRequest)
		{
			string[] allHosts = getAllDomains();
			string wildcardMatch = null;
			foreach (string h in allHosts)
			{
				if (hostCompare(hostFromRequest, h, out bool isExactMatch))
				{
					if (isExactMatch)
						return h;
					else if (wildcardMatch == null)
						wildcardMatch = h;
				}
			}
			return wildcardMatch;
		}
		/// <summary>
		/// Returns true if the given hostname matches the [host] configured here.
		/// </summary>
		/// <param name="hostFromRequest">Hostname requested by the client.</param>
		/// <param name="hostPattern">One host pattern extracted from the Exitpoint.host field.</param>
		/// <param name="isExactMatch">This is set = true only if the host template is a match for the given hostname without using any wildcards.</param>
		/// <returns></returns>
		private static bool hostCompare(string hostFromRequest, string hostPattern, out bool isExactMatch)
		{
			isExactMatch = false;
			if (hostPattern == "*")
				return true;
			string[] hostParts = hostPattern.Split('*');
			if (hostParts.Length > 1)
			{
				string regexQuery = string.Join(".*?", hostParts.Select(p => Regex.Escape(p)));
				regexQuery = "^" + regexQuery + "$";
				return Regex.IsMatch(hostFromRequest, regexQuery);
			}
			else
				return isExactMatch = hostPattern?.IEquals(hostFromRequest) == true;
		}
		/// <summary>
		/// Returns all domains contained in the <see cref="host"/> string.
		/// </summary>
		/// <returns></returns>
		public string[] getAllDomains()
		{
			return host.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
		}
		/// <summary>
		/// Returns a string describing the exitpoint.
		/// </summary>
		/// <returns>A string describing the exitpoint.</returns>
		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	/// <summary>
	/// Defines the type of an Exitpoint.  Can be Disabled, AdminConsole, or WebProxy.
	/// </summary>
	public enum ExitpointType
	{
		/// <summary>
		/// Requests to the Exitpoint will have their connections simply closed.
		/// </summary>
		Disabled = 0,
		/// <summary>
		/// The Exitpoint will serve the web-based administration console.
		/// </summary>
		AdminConsole = 1,
		/// <summary>
		/// Requests to the Exitpoint will be proxied to another host.
		/// </summary>
		WebProxy = 2
	}
}

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
		/// If true, certificates for this host will be obtained and managed automatically via LetsEncrypt.  Automatic certificate management will only work if this host is mapped to an http entrypoint that is reachable on the internet at "http://host:80/".  Wildcards are not allowed in <see cref="host"/> when using <see cref="autoCertificate"/>.
		/// </summary>
		public bool autoCertificate;
		/// <summary>
		/// [Requires autoCertificate == false] Path to the certificate file (pfx).  If null or empty, a default path will be automatically assigned to this field.
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

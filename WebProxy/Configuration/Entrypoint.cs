using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	public class Entrypoint
	{
		/// <summary>
		/// User-defined unique name for this entrypoint.
		/// </summary>
		public string name;
		/// <summary>
		/// IP Address to listen on.  Use null or empty to listen on all interfaces.
		/// </summary>
		public string ipAddress;
		/// <summary>
		/// If between 1 and 65535, this endpoint supports unencrypted http traffic on this TCP port.
		/// </summary>
		public int httpPort;
		/// <summary>
		/// If between 1 and 65535, this endpoint supports TLS / encrypted https traffic on this TCP port.
		/// </summary>
		public int httpsPort;
		/// <summary>
		/// Enum value indicating which set of TLS cipher suites this entrypoint should allow.
		/// </summary>
		[JsonConverter(typeof(StringEnumConverter))]
		public TlsCipherSuiteSet tlsCipherSuiteSet;
		/// <summary>
		/// Array of unique identifiers for middlewares that apply to all requests matching this Entrypoint.
		/// </summary>
		public string[] middlewares;
		/// <summary>
		/// Returns true if the given IPAddress is matched by the [ipAddress] string configured in this Entrypoint.
		/// </summary>
		/// <param name="interfaceAddress">IPAddress of the interface being tested against this Entrypoint.</param>
		/// <param name="isExactMatch">If true, the IP address was an exact match with the [ipAddress] configured in this Entrypoint.  If false, it could be no match or the match could have been because this Entrypoint listens on all interfaces.</param>
		/// <returns></returns>
		public bool isIpMatch(IPAddress interfaceAddress, out bool isExactMatch)
		{
			isExactMatch = false;
			if (string.IsNullOrWhiteSpace(ipAddress))
				return true;
			if (IPAddress.TryParse(ipAddress, out IPAddress myIP))
				isExactMatch = myIP.Equals(interfaceAddress);
			return isExactMatch;
		}
		/// <summary>
		/// Returns true if the http port is between 1 and 65535.
		/// </summary>
		/// <returns></returns>
		public bool httpPortValid()
		{
			return httpPort >= 1 && httpPort <= 65535;
		}
		/// <summary>
		/// Returns true if the https port is between 1 and 65535.
		/// </summary>
		/// <returns></returns>
		public bool httpsPortValid()
		{
			return httpsPort >= 1 && httpsPort <= 65535;
		}
		/// <summary>
		/// Returns a string describing the entrypoint.
		/// </summary>
		/// <returns>A string describing the entrypoint.</returns>
		public override string ToString()
		{
			string ipStr = (string.IsNullOrEmpty(ipAddress) ? "*" : ipAddress);
			List<string> ipEndpoints = new List<string>();
			if (httpPort > 0)
				ipEndpoints.Add("http://" + ipStr + ":" + httpPort);
			if (httpsPort > 0)
				ipEndpoints.Add("https://" + ipStr + ":" + httpsPort);
			return name + (ipEndpoints.Count > 0 ? (" (" + string.Join(" ", ipEndpoints) + ")") : "") + " [" + middlewares.Length + " middleware" + BPUtil.StringUtil.PluralSuffix(middlewares.Length) + "]" + (httpsPort > 0 ? " [TLS: " + tlsCipherSuiteSet + "]" : "");
		}
	}
}

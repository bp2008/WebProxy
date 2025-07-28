using BPUtil;
using BPUtil.SimpleHttp.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	/// <summary>
	/// Enumeration of Middleware types.
	/// </summary>
	public enum MiddlewareType
	{
		/// <summary>
		/// Client connections are dropped if they do not come from an IP address that is listed here.
		/// </summary>
		IPWhitelist,
		/// <summary>
		/// Requests are required to use HTTP Digest Authentication with credentials from this Dictionary.  If multiple HttpDigestAuth middlewares are required for a request, one of them will be chosen arbitrarily.
		/// </summary>
		HttpDigestAuth,
		/// <summary>
		/// <para>Requests using HTTP are automatically redirected to HTTPS on the best supported Entrypoint.</para>
		/// <para>This Middleware only applies to requests that arrive using plain unencrypted HTTP on an Entrypoint that supports both HTTP and HTTPS.</para>
		/// <para>This Middleware does not apply to LetsEncrypt certificate validation requests, which must use unencrypted HTTP.</para>
		/// </summary>
		RedirectHttpToHttps,
		/// <summary>
		/// Requests will have one or more HTTP headers added, removed, or modified.
		/// </summary>
		AddHttpHeaderToRequest,
		/// <summary>
		/// Responses will have one or more HTTP headers added, removed, or modified.
		/// </summary>
		AddHttpHeaderToResponse,
		/// <summary>
		/// All proxied responses will include a Server-Timing header for debugging purposes, showing time taken to connect, send the request, and read the response.
		/// </summary>
		AddProxyServerTiming,
		/// <summary>
		/// Rewrite the "Origin" request header to match the origin written in the exitpoint configuration.  This does not create the "Origin" request header if it was not already present in the client's request.
		/// </summary>
		RewriteOriginRequestHeader,
		/// <summary>
		/// Allows configuration of the X-Forwarded-For header.  Default behavior (if middleware is not enabled) is to drop the header when proxying a request.
		/// </summary>
		XForwardedFor,
		/// <summary>
		/// Allows configuration of the X-Forwarded-Host header.  Default behavior (if middleware is not enabled) is to drop the header when proxying a request.
		/// </summary>
		XForwardedHost,
		/// <summary>
		/// Allows configuration of the X-Forwarded-Proto header.  Default behavior (if middleware is not enabled) is to drop the header when proxying a request.
		/// </summary>
		XForwardedProto,
		/// <summary>
		/// Allows configuration of the X-Real-Ip header.  Default behavior (if middleware is not enabled) is to drop the header when proxying a request.
		/// </summary>
		XRealIp,
		/// <summary>
		/// Allows the caller to provide a list of trusted IP ranges for [XForwardedFor, XForwardedHost, XForwardedProto, XRealIp] middlewares.
		/// </summary>
		TrustedProxyIPRanges,
		/// <summary>
		/// Performs hostname substitution on proxied responses, for text-based responses.  Requires the full response to be buffered.
		/// </summary>
		HostnameSubstitution,
		/// <summary>
		/// Performs Regular Expression replacements on proxied responses, for text-based responses.  Requires the full response to be buffered.
		/// </summary>
		RegexReplaceInResponse
	}
	/// <summary>
	/// Applies additional logic to an Entrypoint or Exitpoint.  Constraints may be applied to an Entrypoint or an Exitpoint or both.
	/// </summary>
	public class Middleware
	{
		/// <summary>
		/// User-defined unique identifier for this middleware.
		/// </summary>
		public string Id = "";
		/// <summary>
		/// The type of this middleware.
		/// </summary>
		[JsonConverter(typeof(StringEnumConverter))]
		public MiddlewareType Type;
		/// <summary>
		/// <para>For [Type] in [IPWhitelist, TrustedProxyIPRanges]</para>
		/// <para>Client connections are dropped if they do not come from an IP address that is listed here.</para>
		/// <para>This is an array of strings defining single IP addresses ("1.1.1.1"), IP ranges ("1.1.1.1 - 1.1.1.5"), or subnets ("1.1.1.0/24"). IPv4 and IPv6 are both supported.</para>
		/// </summary>
		public string[] WhitelistedIpRanges = new string[0];
		/// <summary>
		/// <para>For [Type] = HttpDigestAuth</para>
		/// <para>Requests are required to use HTTP Digest Authentication with credentials from this Dictionary.</para>
		/// <para>This is a dictionary of user name and password that are acceptable.</para>
		/// </summary>
		public List<UnPwCredential> AuthCredentials = new List<UnPwCredential>();
		/// <summary>
		/// <para>For [Type] = AddHttpHeaderToResponse</para>
		/// <para>Responses to our client will have these HTTP headers set (overrides any proxied HTTP headers using the same name).</para>
		/// </summary>
		public string[] HttpHeaders;
		/// <summary>
		/// <para>For [Type] in [XForwardedFor, XForwardedHost, XForwardedProto, XRealIp]</para>
		/// <para>
		/// The header named by [Type] shall be manipulated in the way defined by this field.  If unspecified, the default behavior for all such headers is to drop them.</para>
		/// </summary>
		[JsonConverter(typeof(StringEnumConverter))]
		public ProxyHeaderBehavior ProxyHeaderBehavior = ProxyHeaderBehavior.Drop;
		/// <summary>
		/// <para>For [Type] = HostnameSubstitution</para>
		/// <para>The collection of Hostname pairs to perform replaces with.  For each pair, Key is replaced with Value.</para>
		/// </summary>
		public List<KeyValuePair<string, string>> HostnameSubstitutions = new List<KeyValuePair<string, string>>();
		/// <summary>
		/// <para>For [Type] = RegexReplaceInResponse</para>
		/// <para>The collection of Regular Expression patterns and replacements to perform replaces with.  For each pair, Key is the pattern and Value is the replacement string.</para>
		/// </summary>
		public List<KeyValuePair<string, string>> RegexReplacements = new List<KeyValuePair<string, string>>();
		/// <summary>
		/// Gets the password for the given username, or null.
		/// </summary>
		/// <param name="user">Username</param>
		/// <returns></returns>
		public string GetPassword(string user)
		{
			return AuthCredentials.FirstOrDefault(u => u.User == user)?.Pass;
		}
		/// <summary>
		/// Sets the password for the given user, case-sensitive. If you set a null password, the credential is deleted.
		/// </summary>
		/// <param name="user">Username</param>
		/// <param name="pass">Password. Null to delete the stored credential.</param>
		/// <returns></returns>
		public void SetPassword(string user, string pass)
		{
			int i = AuthCredentials.FindIndex(u => u.User == user);
			UnPwCredential account = i < 0 ? null : AuthCredentials[i];
			if (pass == null)
			{
				if (account == null)
					return;
				else
					AuthCredentials.RemoveAt(i);
			}
			else
			{
				if (account == null)
					AuthCredentials.Add(new UnPwCredential(user, pass));
				else
					account.Pass = pass;
			}
		}
		/// <summary>
		/// Returns a string describing the middleware.
		/// </summary>
		/// <returns>A string describing the middleware.</returns>
		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	/// <summary>
	/// A username and password.
	/// </summary>
	public class UnPwCredential
	{
		/// <summary>
		/// Username
		/// </summary>
		public string User;
		/// <summary>
		/// Password
		/// </summary>
		public string Pass;
		/// <summary>
		/// Constructs an empty UnPwCredential.
		/// </summary>
		public UnPwCredential() { }
		/// <summary>
		/// Constructs a UnPwCredential with the given username and password.
		/// </summary>
		/// <param name="user">Username</param>
		/// <param name="pass">Password</param>

		public UnPwCredential(string user, string pass)
		{
			User = user;
			Pass = pass;
		}
	}
}

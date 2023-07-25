using BPUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	/// <summary>
	/// Gets the credentials and login links for the administration console.
	/// </summary>
	public class AdminInfo
	{
		/// <summary>
		/// The Entrypoint used by the Admin Console.
		/// </summary>
		public Entrypoint adminEntry;
		/// <summary>
		/// The Exitpoint used by the Admin Console.
		/// </summary>
		public Exitpoint adminExit;
		/// <summary>
		/// The authentication middleware used by the Admin Console.
		/// </summary>
		public Middleware adminLoginMiddleware;
		/// <summary>
		/// Username for the admin console user.
		/// </summary>
		public string user;
		/// <summary>
		/// Password for the admin console user.
		/// </summary>
		public string pass;
		/// <summary>
		/// HTTP URL of the admin console, or null.
		/// </summary>
		public string httpUrl;
		/// <summary>
		/// HTTPS URL of the admin console, or null.
		/// </summary>
		public string httpsUrl;
		/// <summary>
		/// The IP address of the admin console (for labeling purposes; this might be a descriptive string instead of an IP address).
		/// </summary>
		public string adminIp;
		/// <summary>
		/// Gets the credentials and login links for the administration console.
		/// </summary>
		public AdminInfo()
		{
			WebProxyService.SettingsValidateAndAdminConsoleSetup(out adminEntry, out adminExit, out adminLoginMiddleware);

			adminIp = string.IsNullOrEmpty(adminEntry.ipAddress) ? "Any IP" : adminEntry.ipAddress;

			string adminHost = adminExit.host;
			adminHost = adminHost?.Replace("*", "");
			if (string.IsNullOrEmpty(adminHost))
				adminHost = "localhost";

			if (adminEntry.httpPortValid())
				httpUrl = "http://" + adminHost + (adminEntry.httpPort == 80 ? "" : (":" + adminEntry.httpPort));

			if (adminEntry.httpsPortValid())
				httpsUrl = "https://" + adminHost + (adminEntry.httpsPort == 443 ? "" : (":" + adminEntry.httpsPort));

			UnPwCredential adminAccount = adminLoginMiddleware.AuthCredentials.FirstOrDefault(c => c.User.IEquals("wpadmin"));
			if (adminAccount == null)
				adminAccount = adminLoginMiddleware.AuthCredentials.FirstOrDefault(c => c.User.IEquals("admin"));
			if (adminAccount == null)
				adminAccount = adminLoginMiddleware.AuthCredentials[0];
			user = adminAccount.User;
			pass = adminAccount.Pass;
		}
	}
}

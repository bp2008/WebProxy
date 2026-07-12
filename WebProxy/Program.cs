using BPUtil;
using BPUtil.Forms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if !LINUX
using System.Windows.Forms;
using WebProxy.ServiceUI;
#endif

namespace WebProxy
{
	static class Program
	{
		/// <summary>
		/// Gets the name of the service, usable for service management.
		/// </summary>
		public static string serviceName { get; private set; }
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static int Main()
		{
			// TODO: Properly support the "Trailer" response header: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Trailer
			// CONSIDER: Add middleware for "Forwarded" header which combines the effects of the previous 3 headers: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Forwarded
			// CONSIDER: Add a middleware that implements a JavaScript-based login form.  Consider supporting WebAuthn or passwordless.id, but the main goal here is to support password manager browser extensions.
			WindowsServiceInitOptions options = new WindowsServiceInitOptions();
#if LINUX
			// Bypass certificate validation for localhost, to allow certain command line interface commands via https.
			options.ServiceName = serviceName = "webproxy";
			options.LinuxCommandLineInterface = runCommandLineInterface;
			options.LinuxOnInstall = runLinuxOnInstallCallback;
#else
			options.ServiceName = serviceName = "WebProxy";
			options.ServiceManagerButtons = new ButtonDefinition[] {
				new ButtonDefinition("Admin Console", (object sender, EventArgs e) =>
				{
					AdminConsoleInfoForm f = new AdminConsoleInfoForm();
					f.ShowDialog();
				}),
				new ButtonDefinition("Remote Plugin Mgmt", (object sender, EventArgs e) =>
				{
					RemotePluginManagementDialog();
				})
			};
#endif
			AppInit.WindowsService<WebProxyService>(options);
			return 0;
		}

#if !LINUX
		/// <summary>
		/// Shows the current state of <see cref="SecureSettings.AllowRemotePluginFileManagement"/> and offers to toggle it.  This is deliberately only reachable from the Service Manager GUI (requiring shell access to the machine); the web-based Admin Console can only disable the flag, never enable it.
		/// </summary>
		private static void RemotePluginManagementDialog()
		{
			const string title = "Remote Plugin File Management";
			if (SecureSettings.GetCurrent().AllowRemotePluginFileManagement)
			{
				if (MessageBox.Show("Remote plugin file management is currently ENABLED."
					+ "\n\nAuthenticated users of the web-based Admin Console are allowed to install and delete plugin DLL files.  Plugins are .NET code which runs with the privileges of the WebProxy service, so this capability is equivalent to remote code execution for anyone who can log into the Admin Console."
					+ "\n\nDo you want to DISABLE remote plugin file management (the secure default)?"
					, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					SecureSettings.SetAllowRemotePluginFileManagement(false);
					MessageBox.Show("Remote plugin file management is now DISABLED.  The change takes effect immediately, even if the service is already running.", title);
				}
			}
			else
			{
				if (MessageBox.Show("Remote plugin file management is currently DISABLED (the secure default).  Plugin files can only be managed by an administrator with access to this machine."
					+ "\n\nEnabling remote plugin file management will allow anyone who can log into the web-based Admin Console to install and delete plugin DLL files.  Plugins are .NET code which runs with the privileges of the WebProxy service, so this is equivalent to granting remote code execution capability to Admin Console users."
					+ "\n\nDo you want to ENABLE remote plugin file management?"
					, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
				{
					SecureSettings.SetAllowRemotePluginFileManagement(true);
					MessageBox.Show("Remote plugin file management is now ENABLED.  The change takes effect immediately, even if the service is already running."
						+ "\n\nIt can be disabled again from this dialog, from the Settings section of the web-based Admin Console, or by editing SecureSettings.json in the data directory.", title);
				}
			}
		}
#endif

#if LINUX
		private static EConsole c = EConsole.I;
		private static void runCommandLineInterface()
		{
			c.CyanLine("Running WebProxy " + Globals.AssemblyVersion + " in command-line mode.");
			c.Line();
			c.Line("Data directory:").CyanLine("\t" + Globals.WritableDirectoryBase);
			c.Line();
			WriteUsage();
			string input = Console.ReadLine();
			while (input != null && input != "exit")
			{
				c.Line();
				if (input == "admin")
				{
					printAdminInfo();
				}
				else if (input == "install")
				{
					AppInit.InstallLinuxSystemdService(serviceName, new WindowsServiceInitOptions() { LinuxOnInstall = runLinuxOnInstallCallback });
				}
				else if (input == "uninstall")
				{
					AppInit.UninstallLinuxSystemdService(serviceName);
				}
				else if (input == "status")
				{
					AppInit.StatusLinuxSystemdService(serviceName);
				}
				else if (input == "start")
				{
					AppInit.StartLinuxSystemdService(serviceName);
				}
				else if (input == "stop")
				{
					AppInit.StopLinuxSystemdService(serviceName);
				}
				else if (input == "restart")
				{
					AppInit.RestartLinuxSystemdService(serviceName);
				}
				else if (input == "readconfig")
				{
					AdminCommandLineInterfaceAPICall("ReadConfig");
				}
				else if (input == "loadconfig")
				{
					AdminCommandLineInterfaceAPICall("LoadConfig");
				}
				else if (input == "saveconfig")
				{
					AdminCommandLineInterfaceAPICall("SaveConfig");
				}
				else if (input == "remoteplugins")
				{
					bool enabled = SecureSettings.GetCurrent().AllowRemotePluginFileManagement;
					c.Write("AllowRemotePluginFileManagement: ");
					if (enabled)
						c.YellowLine("true").Line("Plugin files can be installed and deleted via the web-based Admin Console.  Use the \"disableremoteplugins\" command to return to the secure default.");
					else
						c.GreenLine("false").Line("Plugin files can not be installed or deleted via the web-based Admin Console (the secure default).");
				}
				else if (input == "enableremoteplugins")
				{
					c.YellowLine("Enabling remote plugin file management will allow anyone who can log into the web-based Admin Console to install and delete plugin DLL files.  Plugins are .NET code which runs with the privileges of the WebProxy service, so this is equivalent to granting remote code execution capability to Admin Console users.");
					c.Line();
					c.Write("Type \"yes\" to enable remote plugin file management: ");
					if (Console.ReadLine() == "yes")
					{
						SecureSettings.SetAllowRemotePluginFileManagement(true);
						c.YellowLine("AllowRemotePluginFileManagement is now true.  The change takes effect immediately, even if the service is already running.");
					}
					else
						c.Line("Cancelled.  Remote plugin file management remains in its previous state.");
				}
				else if (input == "disableremoteplugins")
				{
					SecureSettings.SetAllowRemotePluginFileManagement(false);
					c.GreenLine("AllowRemotePluginFileManagement is now false (the secure default).  The change takes effect immediately, even if the service is already running.");
				}
				else
				{
					c.RedLine("Unrecognized command");
				}
				WriteUsage();
				input = Console.ReadLine();
			}
		}
		private static WebRequestUtility wru = new WebRequestUtility("WebProxy Command Line Interface", 4000) { AcceptAnyCertificate = true };
		private static void AdminCommandLineInterfaceAPICall(string methodName)
		{
			try
			{
				AdminInfo adminInfo = new AdminInfo();
				UriBuilder builder = new UriBuilder(adminInfo.httpsUrl ?? adminInfo.httpUrl);
				if (!string.IsNullOrWhiteSpace(adminInfo.adminEntry.ipAddress) && IPAddress.TryParse(adminInfo.adminEntry.ipAddress, out IPAddress ipAddress))
				{
					builder.Host = ipAddress.ToString();
				}
				builder.Path = "/CommandLineInterface/" + methodName;
				if (!string.IsNullOrWhiteSpace(adminInfo.user) && wru.BasicAuthCredentials == null)
					wru.BasicAuthCredentials = new NetworkCredential(adminInfo.user, adminInfo.pass);
				BpWebResponse response = wru.POST(builder.Uri.ToString(), new byte[0], "application/json", new string[] { "X-WebProxy-CSRF-Protection", "1" });
				if (response.StatusCode == 0)
					c.RedLine("Failed to get a response from the service. Is it running? Tried " + builder.Uri.ToString() + " and got " + (response.ex == null ? "No Response" : response.ex.ToHierarchicalString()));
				else if (response.StatusCode != 200)
					c.RedLine("HTTP " + response.StatusCode + " response from admin console: " + response.str);
				else
				{
					dynamic responseData = JsonConvert.DeserializeObject(response.str);
					if (responseData == null)
						c.RedLine("HTTP 200 response from admin console could not be deserialized from JSON: " + response.str);
					else if (responseData.success == true)
						c.Line((string)responseData.message);
					else if (responseData.success == false)
						c.RedLine((string)responseData.error);
					else
						c.YellowLine(response.str);
				}
			}
			catch (Exception ex)
			{
				c.RedLine(ex.ToHierarchicalString());
			}
		}
		private static void WriteUsage()
		{
			c.WriteLine();
			c.WriteLine("Commands:");
			ConsoleAppHelper.MaxCommandSize = 21;
			ConsoleAppHelper.WriteUsageCommand("admin", "Display admin console login link and credentials.");
			ConsoleAppHelper.WriteUsageCommand("install", "Install as service using systemd.");
			ConsoleAppHelper.WriteUsageCommand("uninstall", "Uninstall as service using systemd.");
			ConsoleAppHelper.WriteUsageCommand("status", "Display the service status.");
			ConsoleAppHelper.WriteUsageCommand("start", "Start the service.");
			ConsoleAppHelper.WriteUsageCommand("stop", "Stop the service.");
			ConsoleAppHelper.WriteUsageCommand("restart", "Restart the service.");
			ConsoleAppHelper.WriteUsageCommand("readconfig", "Read current configuration from the running service and display it here.");
			ConsoleAppHelper.WriteUsageCommand("loadconfig", "Instruct the running service to validate and reload the Settings.json file.");
			ConsoleAppHelper.WriteUsageCommand("saveconfig", "Instruct the running service to save its current settings to the Settings.json file.");
			ConsoleAppHelper.WriteUsageCommand("remoteplugins", "Display whether plugin files may be installed/deleted via the web-based Admin Console (SecureSettings.json flag \"AllowRemotePluginFileManagement\").");
			ConsoleAppHelper.WriteUsageCommand("enableremoteplugins", "Allow plugin files to be installed/deleted via the web-based Admin Console (requires confirmation).");
			ConsoleAppHelper.WriteUsageCommand("disableremoteplugins", "Disallow installing/deleting plugin files via the web-based Admin Console (the secure default).");
			ConsoleAppHelper.WriteUsageCommand("exit", "Close this command line interface.");
			c.WriteLine();
		}

		private static void runLinuxOnInstallCallback()
		{
			printAdminInfo();
		}
		private static void printAdminInfo()
		{
			AdminInfo adminInfo = new AdminInfo();
			c.YellowLine("------Credentials------");
			c.Yellow("User: ").WriteLine(adminInfo.user);
			c.Yellow("Pass: ").WriteLine(adminInfo.pass);
			c.YellowLine("---------URLs----------");
			if (adminInfo.httpUrl != null)
				c.Cyan(adminInfo.httpUrl).YellowLine(" (" + adminInfo.adminIp + ")");
			if (adminInfo.httpsUrl != null)
				c.Cyan(adminInfo.httpsUrl).YellowLine(" (" + adminInfo.adminIp + ")");
			c.YellowLine("-----------------------");
		}
#endif
	}
}

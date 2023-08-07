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
using WebProxy.ServiceUI;
#endif

namespace WebProxy
{
	static class Program
	{
#pragma warning disable CS0414
		static string serviceName;
#pragma warning restore
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			// TODO: Log should scroll to bottom after initial load.
			// CONSIDER: Add middleware for "Forwarded" header which combines the effects of the previous 3 headers: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Forwarded
			// CONSIDER: Add a middleware that implements a JavaScript-based login form.  Consider supporting WebAuthn or passwordless.id, but the main goal here is to support password manager browser extensions.
			WindowsServiceInitOptions options = new WindowsServiceInitOptions();
#if LINUX
			options.ServiceName = serviceName = "webproxy";
			options.LinuxCommandLineInterface = runCommandLineInterface;
#else
			options.ServiceName = serviceName = "WebProxy";
			options.ServiceManagerButtons = new ButtonDefinition[] {
				new ButtonDefinition("Admin Console", (object sender, EventArgs e) =>
				{
					AdminConsoleInfoForm f = new AdminConsoleInfoForm();
					f.ShowDialog();
				})
			};
#endif
			AppInit.WindowsService<WebProxyService>(options);
		}

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
				else if (input == "install")
				{
					AppInit.InstallLinuxSystemdService(serviceName);
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
				else
				{
					c.RedLine("Unrecognized command");
				}
				WriteUsage();
				input = Console.ReadLine();
			}
		}
		private static WebRequestUtility wru = new WebRequestUtility("WebProxy Command Line Interface", 4000);
		private static void AdminCommandLineInterfaceAPICall(string methodName)
		{
			try
			{
				AdminInfo adminInfo = new AdminInfo();
				UriBuilder builder = new UriBuilder(adminInfo.httpUrl ?? adminInfo.httpsUrl);
				if (!string.IsNullOrWhiteSpace(adminInfo.adminEntry.ipAddress) && IPAddress.TryParse(adminInfo.adminEntry.ipAddress, out IPAddress ipAddress))
				{
					builder.Host = ipAddress.ToString();
				}
				builder.Path = "/CommandLineInterface/" + methodName;
				if (!string.IsNullOrWhiteSpace(adminInfo.user) && wru.BasicAuthCredentials == null)
					wru.BasicAuthCredentials = new NetworkCredential(adminInfo.user, adminInfo.pass);
				BpWebResponse response = wru.GET(builder.Uri.ToString());
				if (response.StatusCode == 0)
					c.RedLine("Failed to get a response from the service. Is it running?");
				else
				{
					dynamic responseData = JsonConvert.DeserializeObject(response.str);
					if (responseData.success == true)
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
			ConsoleAppHelper.MaxCommandSize = 11;
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
			ConsoleAppHelper.WriteUsageCommand("exit", "Close this command line interface.");
			c.WriteLine();
		}
#endif
	}
}

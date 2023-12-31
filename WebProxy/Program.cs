﻿using BPUtil;
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
				})
			};
#endif
			AppInit.WindowsService<WebProxyService>(options);
			return 0;
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

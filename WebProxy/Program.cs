using BPUtil;
using BPUtil.Forms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			WindowsServiceInitOptions options = new WindowsServiceInitOptions();
#if LINUX
			options.ServiceName = "webproxy";
			options.LinuxCommandLineInterface = runShellInterface;
#else
			options.ServiceName = "WebProxy";
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
		private static void runShellInterface()
		{
			Console.WriteLine("Running WebProxy " + Globals.AssemblyVersion + " in command-line mode.");
			WriteUsage();
			string input = Console.ReadLine();
			while (input != null && input != "exit")
			{
				Console.WriteLine();
				if (input == "admin")
				{
					AdminInfo adminInfo = new AdminInfo();
					Console.WriteLine("------Credentials------");
					Console.WriteLine("User: " + adminInfo.user);
					Console.WriteLine("Pass: " + adminInfo.pass);
					Console.WriteLine("---------URLs----------");
					if (adminInfo.httpUrl != null)
						Console.WriteLine(adminInfo.httpUrl + " (" + adminInfo.adminIp + ")");
					if (adminInfo.httpsUrl != null)
						Console.WriteLine(adminInfo.httpsUrl + " (" + adminInfo.adminIp + ")");
					Console.WriteLine("-----------------------");
				}
				else if (input == "install")
				{
					AppInit.InstallLinuxSystemdService("webproxy");
				}
				else if (input == "uninstall")
				{
					AppInit.UninstallLinuxSystemdService("webproxy");
				}
				else if (input == "viewconfig")
				{
					Console.WriteLine(JsonConvert.SerializeObject(WebProxyService.MakeLocalSettingsReference(), Formatting.Indented));
				}
				else if (input == "loadconfig")
				{
					WebProxyService.InitializeSettings();
				}
				else if (input == "saveconfig")
				{
					WebProxyService.SaveNewSettings(WebProxyService.CloneSettingsObjectSlow());
				}
				else
				{
					Console.WriteLine("Unrecognized command");
				}
				WriteUsage();
				input = Console.ReadLine();
			}
		}

		private static void WriteUsage()
		{
			Console.WriteLine();
			Console.WriteLine("Commands:");
			Console.WriteLine("\t" + "admin      - Display admin console login link and credentials.");
			Console.WriteLine("\t" + "install    - Install as service using systemd.");
			Console.WriteLine("\t" + "uninstall  - Uninstall as service using systemd.");
			Console.WriteLine("\t" + "viewconfig - Display current configuration JSON.");
			Console.WriteLine("\t" + "loadconfig - Reload Settings.json file.");
			Console.WriteLine("\t" + "saveconfig - Rewrite Settings.json file. Can be helpful if ");
			Console.WriteLine("\t" + "             the JSON schema has been changed in an update.");
			Console.WriteLine("\t" + "exit       - Close this program");
			Console.WriteLine();
		}
#endif
	}
}

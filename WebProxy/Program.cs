using BPUtil;
using BPUtil.Forms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
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
			options.LinuxShellInterface = runShellInterface;
#else
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
				else if (input == "config")
				{
					Console.WriteLine(JsonConvert.SerializeObject(WebProxyService.MakeLocalSettingsReference(), Formatting.Indented));
				}
				WriteUsage();
				input = Console.ReadLine();
			}
		}
		private static void WriteUsage()
		{
			Console.WriteLine();
			Console.WriteLine("Commands:");
			Console.WriteLine("\t" + "admin  - Display admin console login link and credentials");
			Console.WriteLine("\t" + "config - Display current configuration JSON");
			Console.WriteLine("\t" + "exit   - Close this program");
			Console.WriteLine();
		}
#endif
	}
}

using BPUtil;
using BPUtil.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using WebProxy.ServiceUI;

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
			options.ServiceManagerButtons = new ButtonDefinition[] {
				new ButtonDefinition("Admin Console", openAdminConsoleInfo)
			};
			AppInit.WindowsService<WebProxyService>(options);
		}

		private static void openAdminConsoleInfo(object sender, EventArgs e)
		{
			AdminConsoleInfoForm f = new AdminConsoleInfoForm();
			f.ShowDialog();
		}
	}
}

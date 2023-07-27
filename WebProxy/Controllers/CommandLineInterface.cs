using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebProxy.Controllers;

namespace WebProxy.Controllers
{
	/// <summary>
	/// Contains methods intended to be used by the Linux command line interface.
	/// </summary>
	public class CommandLineInterface : AdminConsoleControllerBase
	{
		public ActionResult ReadConfig()
		{
			try
			{
				return Json(new CLIResponse(JsonConvert.SerializeObject(WebProxyService.MakeLocalSettingsReference(), Formatting.Indented)));
			}
			catch (Exception ex)
			{
				return ApiError(ex.FlattenMessages());
			}
		}
		public ActionResult LoadConfig()
		{
			try
			{
				WebProxyService.InitializeSettings();
				return Json(new CLIResponse("Success"));
			}
			catch (Exception ex)
			{
				return ApiError(ex.FlattenMessages());
			}
		}
		public ActionResult SaveConfig()
		{
			try
			{
				WebProxyService.SaveNewSettings(WebProxyService.CloneSettingsObjectSlow());
				return Json(new CLIResponse("Success"));
			}
			catch (Exception ex)
			{
				return ApiError(ex.FlattenMessages());
			}
		}
	}
	public class CLIResponse : ApiResponseBase
	{
		public string message;
		public CLIResponse(string message) : base(true)
		{
			this.message = message;
		}
	}
}

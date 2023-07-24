using BPUtil.MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy.Controllers
{
	public abstract class AdminConsoleControllerBase : Controller
	{
		public T ParseRequest<T>()
		{
			return ApiRequest.ParseRequest<T>(this);
		}
		/// <summary>
		/// Returns a JsonResult containing an <see cref="ApiResponseBase"/> that indicates failure and includes the specified error message.
		/// </summary>
		/// <param name="errorMessage">The error message to show to the user.</param>
		/// <returns></returns>
		public JsonResult ApiError(string errorMessage)
		{
			return Json(new ApiResponseBase(false, errorMessage));
		}
	}
}

using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy.Controllers
{
	public abstract class AdminConsoleControllerBase : ControllerAsync
	{
		/// <summary>
		/// When overridden in a derived class, this method may modify any ActionResult before it is sent to the client.
		/// </summary>
		public override async Task PreprocessResult(ActionResult result)
		{
			if (Context.httpProcessor.Request.RequestBodyStream != null)
			{
				ByteUtil.DiscardToEndResult discardResult = await ByteUtil.DiscardUntilEndOfStreamWithMaxLengthAsync(Context.httpProcessor.Request.RequestBodyStream, ApiRequest.RequestBodySizeLimit, 5000, CancellationToken).ConfigureAwait(false);
				if (!discardResult.EndOfStream)
				{
					WebProxyService.ReportError("AdminConsoleControllerBase.PreprocessResult() found more than " + ApiRequest.RequestBodySizeLimit + " bytes unread in the request body.\r\n"
						+ "Client IP: " + this.Context.httpProcessor.RemoteIPAddressStr + "\r\n"
						+ "Request Path: " + this.Context.Path + "\r\n"
						+ "Controller type: " + this.GetType().Name);
				}
			}
		}

		public async Task<T> ParseRequest<T>(CancellationToken cancellationToken = default)
		{
			return await ApiRequest.ParseRequest<T>(this, cancellationToken).ConfigureAwait(false);
		}
		/// <summary>
		/// Returns a JsonResult containing an <see cref="ApiResponseBase"/> that indicates failure and includes the specified error message.
		/// </summary>
		/// <param name="errorMessage">The error message to show to the user.</param>
		/// <returns></returns>
		public JsonResult ApiError(string errorMessage)
		{
			return new JsonResult(new ApiResponseBase(false, errorMessage)) { ResponseStatus = "418 Error But Prevent Autocomplete" };
		}
		/// <summary>
		/// Returns a JsonResult containing an <see cref="ApiResponseBase"/> that indicates failure and includes the specified error message. This result specifies that a non-2xx HTTP response status code should be used in order to prevent autocompelte.
		/// </summary>
		/// <param name="obj">Result object that should be serialized as JSON.</param>
		/// <returns></returns>
		public JsonResult ApiSuccessNoAutocomplete(ApiResponseBase obj)
		{
			return new JsonResult(obj) { ResponseStatus = "418 Success But Prevent Autocomplete" };
		}
		/// <summary>
		/// Returns a JsonResult containing an <see cref="ApiResponseBase"/> that indicates failure and includes the specified error message.
		/// </summary>
		/// <param name="errorMessage">The error message to show to the user.</param>
		/// <returns></returns>
		public Task<ActionResult> ApiErrorTask(string errorMessage)
		{
			return Task.FromResult<ActionResult>(ApiError(errorMessage));
		}
		/// <summary>
		/// Returns a JsonResult containing an <see cref="ApiResponseBase"/> that indicates failure and includes the specified error message. This result specifies that a non-2xx HTTP response status code should be used in order to prevent autocompelte.
		/// </summary>
		/// <param name="obj">Result object that should be serialized as JSON.</param>
		/// <returns></returns>
		public Task<ActionResult> ApiSuccessNoAutocompleteTask(ApiResponseBase obj)
		{
			return Task.FromResult<ActionResult>(ApiSuccessNoAutocomplete(obj));
		}
	}
}

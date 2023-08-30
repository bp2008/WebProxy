using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy
{
	public static class ApiRequest
	{
		/// <summary>
		/// Maximum size of a Request Body, in bytes.
		/// </summary>
		public const int RequestBodySizeLimit = 20 * 1024 * 1024;
		/// <summary>
		/// Parses an API request argument (JSON) from the HTTP POST body.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="controller">The <see cref="Controller"/> you are calling from. ("this")</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns></returns>
		public static async Task<T> ParseRequest<T>(Controller controller, CancellationToken cancellationToken = default)
		{
			return await ParseRequest<T>(controller.Context.httpProcessor, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Parses an API request argument (JSON) from the HTTP POST body.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="httpProcessor">The <see cref="HttpProcessor"/> which is handling the API request.</param>
		/// <param name="cancellationToken">Cancellation Token</param>
		/// <returns></returns>
		public static async Task<T> ParseRequest<T>(HttpProcessor httpProcessor, CancellationToken cancellationToken = default)
		{
			if (httpProcessor.http_method != "POST")
				throw new Exception("This API method must be called using HTTP POST");
			ByteUtil.AsyncReadResult result = await ByteUtil.ReadToEndWithMaxLengthAsync(httpProcessor.RequestBodyStream, RequestBodySizeLimit, cancellationToken).ConfigureAwait(false);
			if (result.EndOfStream)
			{
				string str = ByteUtil.Utf8NoBOM.GetString(result.Data);
				return JsonConvert.DeserializeObject<T>(str);
			}
			else
			{
				if (!httpProcessor.responseWritten)
					await httpProcessor.writeFailureAsync("413 Content Too Large", "This server allows a maximum request body size of " + RequestBodySizeLimit + " bytes.", cancellationToken: cancellationToken).ConfigureAwait(false);
				throw new Exception("413 Content Too Large: This server allows a maximum request body size of " + RequestBodySizeLimit + " bytes.");
			}
		}
	}
}

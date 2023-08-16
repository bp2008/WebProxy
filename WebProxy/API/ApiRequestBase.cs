using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
		/// <returns></returns>
		public static T ParseRequest<T>(Controller controller)
		{
			return ParseRequest<T>(controller.Context.httpProcessor);
		}

		/// <summary>
		/// Parses an API request argument (JSON) from the HTTP POST body.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="httpProcessor">The <see cref="HttpProcessor"/> which is handling the API request.</param>
		/// <returns></returns>
		public static T ParseRequest<T>(HttpProcessor httpProcessor)
		{
			if (httpProcessor.http_method != "POST")
				throw new Exception("This API method must be called using HTTP POST");

			if (ByteUtil.ReadToEndWithMaxLength(httpProcessor.RequestBodyStream, RequestBodySizeLimit, out byte[] data))
			{
				string str = ByteUtil.Utf8NoBOM.GetString(data);
				return JsonConvert.DeserializeObject<T>(str);
			}
			else
			{
				if (!httpProcessor.responseWritten)
					httpProcessor.writeFailure("413 Content Too Large", "This server allows a maximum request body size of " + RequestBodySizeLimit + " bytes.");
				throw new Exception("413 Content Too Large: This server allows a maximum request body size of " + RequestBodySizeLimit + " bytes.");
			}
		}
	}
}

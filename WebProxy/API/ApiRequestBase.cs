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

			byte[] data = httpProcessor.PostBodyStream.ToArray();
			string str = ByteUtil.Utf8NoBOM.GetString(data);
			return JsonConvert.DeserializeObject<T>(str);
		}
	}
}

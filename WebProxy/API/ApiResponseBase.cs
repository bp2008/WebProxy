using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	public class ApiResponseBase
	{
		public bool success;
		public string error;

		public ApiResponseBase(bool success, string error = null)
		{
			if (success && !string.IsNullOrEmpty(error))
				throw new Exception("API Response indicated success but contained an error message.");
			if (!success && string.IsNullOrEmpty(error))
				throw new Exception("API Response indicated failure but contained no error message.");

			this.success = success;
			this.error = error;
		}
	}
}

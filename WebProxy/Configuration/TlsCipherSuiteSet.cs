using BPUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	/// <summary>
	/// <para>Specifies which set of TLS cipher suites should be allowed.</para>
	/// <para>If you are going to rename or remove an enum value that was previously defined, please take care to ensure that existing configurations with the changed value do not crash.</para>
	/// </summary>
	public enum TlsCipherSuiteSet
	{
		/// <summary>
		/// Use default cipher suites (varies by machine/OS).
		/// </summary>
		[Description("Use default cipher suites (varies by machine/OS).")]
		PlatformDefault,
		/// <summary>
		/// Use cipher suites defined by Microsoft here for .NET 5+ on August 8th, 2023: https://learn.microsoft.com/en-us/dotnet/core/compatibility/cryptography/5.0/default-cipher-suites-for-tls-on-linux
		/// </summary>
		[Description("Use cipher suites defined by Microsoft here for .NET 5+ on August 8th, 2023: https://learn.microsoft.com/en-us/dotnet/core/compatibility/cryptography/5.0/default-cipher-suites-for-tls-on-linux")]
		DotNet5_Q3_2023,
		/// <summary>
		/// Use cipher suites defined by IANA here on August 8th, 2023: https://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml
		/// </summary>
		[Description("Use cipher suites defined by IANA here on August 8th, 2023: https://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml")]
		IANA_Q3_2023
	}
}

using BPUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy.LetsEncrypt
{
	public class ManagedCertificate
	{
		/// <summary>
		/// The hostname which this ManagedCertificate instance is for.  Multiple ManagedCertificate instances may point at the same file on disk if the Certificate supports multiple hostnames.
		/// </summary>
		public readonly string Host;
		/// <summary>
		/// Gets the certificate.
		/// </summary>
		public X509Certificate Certificate { get; private set; } = null;
		public DateTime ExpirationDate { get; private set; } = TimeUtil.DateTimeFromEpochMS(0);
	}
}

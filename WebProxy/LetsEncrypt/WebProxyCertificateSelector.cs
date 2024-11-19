using BPUtil;
using BPUtil.SimpleHttp;
using BPUtil.SimpleHttp.TLS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy.LetsEncrypt
{
	public class WebProxyCertificateSelector : ICertificateSelector
	{
		/// <summary>
		/// Returns a special X509Certificate (or null) in order to complete "acme-tls/1" or "TLS-ALPN-01" validation.
		/// </summary>
		/// <param name="p">The HttpProcessor instance in its current early state of processing.  Many fields have not been initialized yet.</param>
		/// <param name="serverName">The server name as indicated by ServerNameIndication.  This is a required parameter and should not be null or empty.</param>
		/// <returns>an X509Certificate or null</returns>
		public Task<X509Certificate> GetAcmeTls1Certificate(HttpProcessor p, string serverName)
		{
			// Ignore all configuration for Entrypoints, Exitpoints, Middlewares, etc.  This is only for "TLS-ALPN-01" validation.  If there's no certificate prepared, the connection will simply be closed.
			X509Certificate cert = CertMgr.GetAcmeTls1Certificate(serverName);
			Logger.Info("ACME TLS-ALPN-01: " + p.RemoteIPAddressStr + " -> " + serverName + ": " + (cert == null ? "NO CERT WAS PREPARED" : "X509Certificate"));
			return Task.FromResult(cert);
		}

		/// <summary>
		/// Returns an X509Certificate appropriate for the specified serverName, or null to indicate no certificate is available (the connection will be closed).
		/// </summary>
		/// <param name="p">The HttpProcessor instance in its current early state of processing.  Many fields have not been initialized yet.</param>
		/// <param name="serverName">The server name as indicated by ServerNameIndication. May be null or empty. Not case sensitive.</param>
		/// <returns>an X509Certificate or null</returns>
		public async Task<X509Certificate> GetCertificate(HttpProcessor p, string serverName)
		{
			if (serverName == null)
				serverName = "";

			Settings settings = WebProxyService.MakeLocalSettingsReference();
			Entrypoint[] matchedEntrypoints = settings.identifyThisEntrypoint(p.RemoteEndPoint, p.LocalEndPoint, true);
			if (matchedEntrypoints.Length == 0)
			{
				WebProxyService.ReportError("WebProxyCertificateSelector: Unable to identify any matching entrypoint for request from client " + p.RemoteIPAddressStr + " to " + p.Request.Url);
				return null;
			}

			Exitpoint myExitpoint = settings.identifyThisExitpoint(matchedEntrypoints, p, out Entrypoint myEntrypoint);
			if (myExitpoint == null || myExitpoint.type == ExitpointType.Disabled)
			{
				// We want this connection to simply close.
				return null;
			}

			IEnumerable<Middleware> allApplicableMiddlewares = settings.middlewares
				.Where(m => myEntrypoint.middlewares?.Contains(m.Id) == true
							|| myExitpoint.middlewares?.Contains(m.Id) == true);

			if (!WebServer.IPWhitelistCheck(p.TrueRemoteIPAddress, allApplicableMiddlewares))
				return null;

			if (myExitpoint.autoCertificate)
			{
				try
				{
					return await CertMgr.GetCertificate(serverName, myEntrypoint, myExitpoint, true).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					WebProxyService.ReportError(ex);
					return null;
				}
			}
			else
			{
				// The certificate is provided manually via filesystem path.
				string certPath = myExitpoint.certificatePath;
				if (string.IsNullOrWhiteSpace(certPath))
				{
					Settings newSettings = WebProxyService.CloneSettingsObjectSlow();
					myExitpoint = newSettings.exitpoints.First(e => e.name == myExitpoint.name);
					string[] domains = GetDomainsForSelfSignedCert(myExitpoint);
					myExitpoint.certificatePath = certPath = CertMgr.GetDefaultCertificatePath(domains[0]);
					await WebProxyService.SaveNewSettings(newSettings).ConfigureAwait(false);
				}

				if (!staticCertDict.TryGetValue(certPath, out ReloadingCertificateSelector cached))
				{
					certPath = GetCertPath(myExitpoint);
					CertificatePfxInfo cpi = new CertificatePfxInfo(certPath, null);
					staticCertDict[certPath] = cached = new ReloadingCertificateSelector(() => cpi);
				}

				return await cached.GetCertificate(p, serverName);
			}
		}
		private static string[] GetDomainsForSelfSignedCert(Exitpoint exitpoint)
		{
			string[] domains = exitpoint.getAllDomains()
				.Select(d => d.Trim())
				.Where(d => !string.IsNullOrEmpty(d))
				.Distinct()
				.ToArray();
			if (domains.Length == 0)
				domains = new string[] { "*" };
			return domains;
		}
		/// <summary>
		/// Returns the path of a certificate file, if one exists and is ready to read.  This method generates a self-signed certificate if necessary.  Returns null if the certificate is not available and self-signed generation is disabled.
		/// </summary>
		/// <param name="exitpoint">The Exitpoint for which to get the certificate.</param>
		/// <returns></returns>
		private static string GetCertPath(Exitpoint exitpoint)
		{
			string[] domains = null;
			string certPath = exitpoint.certificatePath;
			if (string.IsNullOrWhiteSpace(certPath))
			{
				domains = GetDomainsForSelfSignedCert(exitpoint);
				certPath = CertMgr.GetDefaultCertificatePath(domains[0]);
			}
			if (!File.Exists(certPath))
			{
				if (!exitpoint.allowGenerateSelfSignedCertificate)
					return null;

				if (domains == null)
					domains = GetDomainsForSelfSignedCert(exitpoint);

				lock (selfSignedCertLock)
				{
					if (!File.Exists(certPath))
					{
						using (System.Security.Cryptography.RSA key = System.Security.Cryptography.RSA.Create(2048))
						{
							CertificateRequest request = new CertificateRequest("cn=" + domains[0], key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);

							SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
							foreach (string domain in domains)
								sanBuilder.AddDnsName(domain);
							request.CertificateExtensions.Add(sanBuilder.Build());

							X509Certificate2 ssl_certificate = request.CreateSelfSigned(DateTime.Today.AddDays(-1), DateTime.Today.AddYears(100));

							byte[] certData = ssl_certificate.Export(X509ContentType.Pfx);
							FileInfo fiCert = new FileInfo(certPath);
							Robust.RetryPeriodic(() =>
							{
								Directory.CreateDirectory(fiCert.Directory.FullName);
								File.WriteAllBytes(fiCert.FullName, certData);
							}, 50, 6);

							return certPath;
						}
					}
				}
			}
			// If we get here, the certificate file was just confirmed to exist.
			return certPath;
		}
		private static object selfSignedCertLock = new object();
		private ConcurrentDictionary<string, ReloadingCertificateSelector> staticCertDict = new ConcurrentDictionary<string, ReloadingCertificateSelector>();
	}
}

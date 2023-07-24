using BPUtil;
using BPUtil.SimpleHttp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
			Entrypoint[] matchedEntrypoints = settings.identifyThisEntrypoint((IPEndPoint)p.tcpClient.Client.RemoteEndPoint, (IPEndPoint)p.tcpClient.Client.LocalEndPoint, true);
			if (matchedEntrypoints.Length == 0)
			{
				Logger.Info("WebProxyCertificateSelector: Unable to identify any matching entrypoint for request from client " + p.RemoteIPAddressStr + " to " + p.request_url);
				return null;
			}

			Exitpoint myExitpoint = settings.identifyThisExitpoint(matchedEntrypoints, p, out Entrypoint myEntrypoint);
			if (myExitpoint == null || myExitpoint.type == ExitpointType.Disabled)
			{
				// Set responseWritten = true to prevent a fallback response.  We want this connection to simply close.
				Logger.Info("WebProxyCertificateSelector: No exitpoint for request from client " + p.RemoteIPAddressStr + " to " + p.request_url);
				return null;
			}

			if (myExitpoint.autoCertificate)
			{
				try
				{
					return await CertMgr.GetCertificate(serverName, myEntrypoint, myExitpoint);
				}
				catch (Exception ex)
				{
					Logger.Debug(ex);
					return null;
				}
			}
			else
			{
				// The certificate is provided manually via filesystem path.
				string certPath = myExitpoint.certificatePath;
				if (string.IsNullOrWhiteSpace(certPath))
				{
					Logger.Info("Certificate requested for exitpoint with null or whitespace certificatePath.");
					return null;
				}
				if (!staticCertDict.TryGetValue(certPath, out CachedObject<X509Certificate2> cache))
				{
					// Construct a CachedObject so this certificate does not need to be loaded from disk for every request.
					staticCertDict[certPath] = cache = new CachedObject<X509Certificate2>(
						() =>
						{
							try
							{
								if (File.Exists(certPath))
									return new X509Certificate2(certPath);
							}
							catch (Exception ex)
							{
								Logger.Debug(ex);
								return null;
							}
							return null;
						}
						, TimeSpan.FromSeconds(10)
						, TimeSpan.FromSeconds(60)
					);
				}
				return cache.GetInstance();
			}
		}
		private ConcurrentDictionary<string, CachedObject<X509Certificate2>> staticCertDict = new ConcurrentDictionary<string, CachedObject<X509Certificate2>>();
	}
}

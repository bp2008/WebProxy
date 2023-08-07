using BPUtil;
using Certes;
using Certes.Acme;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy.LetsEncrypt
{
	/// <summary>
	/// Manages certificates from LetsEncrypt.
	/// </summary>
	public static class CertMgr
	{
		/// <summary>
		/// Enter this semaphore while accessing <see cref="acme"/>.
		/// </summary>
		private static SemaphoreSlim myLock = new SemaphoreSlim(1, 1);
		private static AcmeContext acme = null;
		/// <summary>
		/// Gets the last error from CertMgr, for display in the admin console.
		/// </summary>
		public static string LastError { get; private set; } = null;
		/// <summary>
		/// The URI of the ACME server where certificates will be obtained from.
		/// </summary>
		private static Uri acmeServerUri = WellKnownServers.LetsEncryptV2;
		/// <summary>
		/// The last email address in use when Initialize was called.
		/// </summary>
		private static string LastEmail = null;
		private static async void Initialize()
		{
			Settings settings = WebProxyService.MakeLocalSettingsReference();
			if (string.IsNullOrWhiteSpace(settings.acmeAccountEmail))
				throw new ApplicationException("LetsEncrypt Account Email has not been specified. Unable to use automatic certificate management.");

			if (acme != null && LastEmail == settings.acmeAccountEmail)
				return;

			await myLock.WaitAsync();
			try
			{
				settings = WebProxyService.MakeLocalSettingsReference();
				if (string.IsNullOrWhiteSpace(settings.acmeAccountEmail))
					throw new ApplicationException("LetsEncrypt Account Email has not been specified. Unable to use automatic certificate management.");

				if (acme != null && LastEmail == settings.acmeAccountEmail)
					return;

				if (string.IsNullOrWhiteSpace(settings.acmeAccountKey))
				{
					Logger.Info("CertMgr.Initialize: Create LetsEncrypt" + (acmeServerUri == WellKnownServers.LetsEncryptStagingV2 ? " Staging" : "") + " Account (" + settings.acmeAccountEmail + ")");
					acme = new AcmeContext(acmeServerUri);
					IAccountContext account = await acme.NewAccount(settings.acmeAccountEmail, true);

					// Save the account key for later use
					settings = WebProxyService.CloneSettingsObjectSlow();
					settings.acmeAccountKey = acme.AccountKey.ToPem();
					WebProxyService.SaveNewSettings(settings);
				}
				else
				{
					Logger.Info("CertMgr.Initialize: Use existing LetsEncrypt" + (acmeServerUri == WellKnownServers.LetsEncryptStagingV2 ? " Staging" : "") + " Account (" + settings.acmeAccountEmail + ")");
					IKey accountKey = KeyFactory.FromPem(settings.acmeAccountKey);
					acme = new AcmeContext(acmeServerUri, accountKey);
					IAccountContext accountContext = await acme.Account();
					Certes.Acme.Resource.Account account = await accountContext.Resource();
					string emailContact = "mailto:" + settings.acmeAccountEmail;
					if (account.Contact.Count < 1 || account.Contact[0] != emailContact)
					{
						List<string> newContact = new List<string>(new string[] { emailContact });
						Logger.Info("CertMgr.Initialize: Changing LetsEncrypt Account Contact from \"" + string.Join(",", account.Contact) + "\" to \"" + string.Join(",", newContact) + "\"");
						await accountContext.Update(newContact, true);
					}
				}
				LastEmail = settings.acmeAccountEmail;
			}
			catch (Exception ex)
			{
				acme = null;
				SetLastError(ex);
				ex.Rethrow();
			}
			finally
			{
				myLock.Release();
			}
		}

		/// <summary>
		/// Renews if necessary, then returns the automatically-generated certificate for this connection or throws an exception if unable.
		/// </summary>
		/// <param name="host">Hostname requested by the client.</param>
		/// <param name="entrypoint">Entrypoint</param>
		/// <param name="exitpoint">Exitpoint</param>
		/// <returns></returns>
		public static async Task<X509Certificate> GetCertificate(string host, Entrypoint entrypoint, Exitpoint exitpoint)
		{
			string[] domains = ValidateRequest(entrypoint, exitpoint);

			host = domains.FirstOrDefault(d => d.IEquals(host));

			if (string.IsNullOrWhiteSpace(host) || host.Contains('/') || host.Contains('\\'))
				throw new ArgumentException("host is invalid");

			// Try to get existing certificate.
			CachedObject<X509Certificate2> cache = GetCertCache(host);

			bool shouldRenew = ShouldRenew(cache, domains);
			// ShouldRenew forces a reload of the cache if there's no cert, so it should be called before we try to get the cached instance.
			X509Certificate2 cert = cache.GetInstance();
			if (cert == null)
			{
				if (shouldRenew)
					Logger.Info("CertMgr: Create now " + host + " (" + string.Join(" ", domains) + ")");
				// Create new certificate
				if (shouldRenew)
					await CreateCertificateForDomains(entrypoint, exitpoint);
				return cache.Reload();
			}
			else
			{
				if (shouldRenew)
				{
					// Create new certificate in background
					Logger.Info("CertMgr: Renew async " + host + " (" + string.Join(" ", domains) + ")");
					_ = CreateCertificateForDomains(entrypoint, exitpoint);
				}
				return cert;
			}
		}

		/// <summary>
		/// Forcibly renews the certificate for this connection, restarting the regular cooldown. Returns null if successful, otherwise an error message.
		/// </summary>
		/// <param name="entrypoint">Entrypoint</param>
		/// <param name="exitpoint">Exitpoint</param>
		/// <returns></returns>
		public static async Task<string> ForceRenew(Entrypoint entrypoint, Exitpoint exitpoint)
		{
			try
			{
				string[] domains = ValidateRequest(entrypoint, exitpoint);

				lock (renewCheckLock)
					renewalCooldowns[domains[0]] = Stopwatch.StartNew();

				Logger.Info("CertMgr: Force renew " + string.Join(" ", domains));

				bool success = await CreateCertificateForDomains(entrypoint, exitpoint);
				if (!success)
				{
					string err = LastError;
					if (err == null)
						err = "Unknown Error";
					return err;
				}
				return null;
			}
			catch (Exception ex)
			{
				return ex.ToHierarchicalString();
			}
		}
		#region Renewal
		private static object renewCheckLock = new object();
		private static Dictionary<string, Stopwatch> renewalCooldowns = new Dictionary<string, Stopwatch>();
		/// <summary>
		/// Returns true if now is a good time for this thread to renew the certificate.
		/// If this method returns true, it is important that a renewal is attempted because there's a cooldown before the method will return true again.
		/// </summary>
		/// <param name="cache">CachedObject responsible for loading the certificate from disk.</param>
		/// <param name="domains">The array of domains belonging to this certificate.</param>
		/// <returns></returns>
		private static bool ShouldRenew(CachedObject<X509Certificate2> cache, string[] domains)
		{
			X509Certificate2 cert = cache.GetInstance();
			if (cert == null)
				cert = cache.Reload();

			TimeSpan cooldownTime;
			if (cert == null)
				cooldownTime = TimeSpan.FromHours(4); // Retry every 4 hours until certificate is generated.
			else
			{
				DateTime expDate = DateTime.Parse(cert.GetExpirationDateString());
				if (expDate <= DateTime.Now.AddDays(1))
					cooldownTime = TimeSpan.FromHours(1); // Expiring within 1 day. Attempt to renew every hour.
				else if (expDate <= DateTime.Now.AddMonths(1))
					cooldownTime = TimeSpan.FromDays(1); // Expiring within 1 month. Attempt to renew every day.
				else
					return false; // No need to renew
			}
			lock (renewCheckLock)
			{
				Settings settings = WebProxyService.MakeLocalSettingsReference();
				if (renewalCooldowns.TryGetValue(domains[0], out Stopwatch sw))
				{
					if (sw.Elapsed > cooldownTime)
					{
						renewalCooldowns[domains[0]] = Stopwatch.StartNew();
						return true;
					}
				}
				else
				{
					renewalCooldowns[domains[0]] = Stopwatch.StartNew();
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Creates a new certificate which will include all of the specified domains, then returns true. If this method fails, it will log an exception and return false.
		/// </summary>
		/// <returns></returns>
		private static async Task<bool> CreateCertificateForDomains(Entrypoint entrypoint, Exitpoint exitpoint)
		{
			try
			{
				// If true, the server is expected to be listening on standard HTTP port 80.  HTTP-01 validation is supported.
				bool standardHttpSupported = entrypoint.httpPort == 80;
				// If true, the server is expected to be listening on standard HTTPS port 443.  TLS-ALPN-01 validation is supported.
				bool standardHttpsSupported = entrypoint.httpsPort == 443;
				// If true, and DNS-01 validation is given as a validation choice, we will attempt to use it.
				bool dns01Cloudflare = exitpoint.cloudflareDnsValidation;
				// Domains to include in the certificate.
				string[] domains = exitpoint.getAllDomains();

				Settings settings = WebProxyService.MakeLocalSettingsReference();
				string cloudflareApiToken = null;
				if (dns01Cloudflare)
					cloudflareApiToken = settings.cloudflareApiToken;
				if (!standardHttpSupported && !standardHttpsSupported && !dns01Cloudflare)
					throw new ArgumentException("Certificate creation will not be attempted because no ACME validation method is currently possible. Ensure that this server is listening on public port 80 (http), 443 (https), or that DNS validation is properly configured.");

				Initialize();


				await myLock.WaitAsync();
				try
				{
					// Create order for certificate.
					Logger.Info("CertMgr.Create: order");
					IOrderContext orderContext = await acme.NewOrder(domains);
					Certes.Acme.Resource.Order order = await orderContext.Resource();
					IEnumerable<IAuthorizationContext> authorizations = await orderContext.Authorizations();
					IKey alpnCertKey = null;
					foreach (IAuthorizationContext authz in authorizations)
					{
						Certes.Acme.Resource.Authorization authResource = await authz.Resource();
						string domain = authResource.Identifier.Value;
						if (authResource.Status == Certes.Acme.Resource.AuthorizationStatus.Valid)
						{
							Logger.Info("CertMgr.Create: " + domain + " is already validated with expiration: " + authResource.Expires);
							continue;
						}
						IEnumerable<IChallengeContext> allChallenges = await authz.Challenges();
						Logger.Info("CertMgr.Create: " + domain + " can be validated via: " + string.Join(", ", allChallenges.Select(cc => cc.Type)));

						// Validate domain ownership.
						if (dns01Cloudflare)
						{
							IChallengeContext challengeContext = await authz.Dns();
							if (challengeContext != null)
							{
								Logger.Info("CertMgr.Create: " + domain + " starting validation via " + challengeContext.Type);
								string dnsKey = "_acme-challenge." + domain;
								string dnsValue = acme.AccountKey.DnsTxt(challengeContext.Token);
								try
								{
									await CloudflareDnsValidator.CreateDNSRecord(dnsKey, dnsValue);
									Logger.Info("CertMgr.Create Cloudflare-DNS-01: " + dnsKey + " TXT record created");

									Certes.Acme.Resource.Challenge challenge = await ValidateAndWait(challengeContext);
									continue;
								}
								finally
								{
									try
									{
										await CloudflareDnsValidator.DeleteDNSRecord(dnsKey);
										Logger.Info("CertMgr.Create Cloudflare-DNS-01: " + dnsKey + " TXT record deleted");
									}
									catch (Exception ex)
									{
										Logger.Info("CertMgr.Create Cloudflare-DNS-01: " + dnsKey + " TXT record failed to delete: " + ex.ToHierarchicalString());
									}
								}
							}
						}
						if (standardHttpSupported)
						{
							IChallengeContext challengeContext = await authz.Http();
							if (challengeContext != null)
							{
								Logger.Info("CertMgr.Create: " + domain + " starting validation via " + challengeContext.Type);
								try
								{
									SetupHttpChallenge(domain, challengeContext.Token, challengeContext.KeyAuthz);

									Certes.Acme.Resource.Challenge challenge = await ValidateAndWait(challengeContext);
									continue;
								}
								finally
								{
									ClearHttpChallenge(domain, challengeContext.Token);
								}
							}
						}
						if (standardHttpsSupported)
						{
							IChallengeContext challengeContext = await authz.TlsAlpn();
							if (challengeContext != null)
							{
								if (alpnCertKey == null)
									alpnCertKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
								Logger.Info("CertMgr.Create: " + domain + " starting validation via " + challengeContext.Type);
								try
								{
									SetupTlsAlpn01Challenge(challengeContext.Token, domain, alpnCertKey);

									Certes.Acme.Resource.Challenge challenge = await ValidateAndWait(challengeContext);
									continue;
								}
								finally
								{
									ClearTlsAlpn01Challenge(domain);
								}
							}
						}
					}

					// Generate new certificate.
					Logger.Info("CertMgr.Create: Generate");
					IKey privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
					CsrInfo csrInfo = new CsrInfo();
					csrInfo.CommonName = domains[0];
					CertificateChain cert = await orderContext.Generate(csrInfo, privateKey);
					Certes.Pkcs.PfxBuilder pfxBuilder = cert.ToPfx(privateKey);
					if (acmeServerUri == WellKnownServers.LetsEncryptStagingV2)
					{
						DirectoryInfo diCerts = new DirectoryInfo(Path.Combine(WebServer.projectDirPath, "StagingCerts"));
						if (diCerts.Exists)
						{
							Logger.Info("CertMgr.Create: Load LetsEncryptStagingV2 certificates from \"" + diCerts.FullName + "\".  If certificate building fails after this, download new certificates from https://github.com/letsencrypt/website/tree/master/static/certs/staging");
							foreach (FileInfo fi in diCerts.GetFiles("*.pem"))
								pfxBuilder.AddIssuer(File.ReadAllBytes(fi.FullName));
						}
						else
						{
							Logger.Info("CertMgr.Create: StagingCerts directory not found.  The pfx output will not include the full chain.");
							pfxBuilder.FullChain = false;
						}
					}
					byte[] pfx = pfxBuilder.Build(domains[0], "");

					Logger.Info("CertMgr.Create: Save");
					Robust.RetryPeriodic(() =>
					{
						Directory.CreateDirectory(CertsBaseDir);
					}, 50, 100);

					foreach (string domain in domains)
					{
						Robust.RetryPeriodic(() =>
						{
							File.WriteAllBytes(GetDefaultCertificatePath(domain), pfx);
						}, 50, 100);
					}
				}
				finally
				{
					myLock.Release();
				}
				return true;
			}
			catch (Exception ex)
			{
				SetLastError(ex);
				return false;
			}
		}

		/// <summary>
		/// Commands the ACME server to validate the challenge, then waits for the validation to complete.
		/// </summary>
		/// <param name="challenge"></param>
		/// <returns></returns>
		private static async Task<Certes.Acme.Resource.Challenge> ValidateAndWait(IChallengeContext challenge)
		{
			Certes.Acme.Resource.Challenge result = await challenge.Validate();
			CountdownStopwatch sw = CountdownStopwatch.StartNew(TimeSpan.FromMinutes(1));
			while (!sw.Finished && result.Status == Certes.Acme.Resource.ChallengeStatus.Pending || result.Status == Certes.Acme.Resource.ChallengeStatus.Processing)
			{
				result = await challenge.Resource();
				await Task.Delay(500);
			}
			Logger.Info("CertMgr.Create: validation challenge status: " + result.Status);
			if (result.Error != null)
				throw new AcmeException(result.Error);
			return result;
		}
		#endregion
		#region HTTP Challenge
		private static ConcurrentDictionary<string, ConcurrentDictionary<string, string>> httpChallenges = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
		/// <summary>
		/// Remembers a file for http challenges.
		/// </summary>
		/// <param name="domain">Domain where the file belongs</param>
		/// <param name="fileName">File name</param>
		/// <param name="fileValue">Value inside the file</param>
		private static void SetupHttpChallenge(string domain, string fileName, string fileValue)
		{
			ConcurrentDictionary<string, string> files = httpChallenges.GetOrAdd(domain, d => { return new ConcurrentDictionary<string, string>(); });
			files[fileName] = fileValue;
		}
		/// <summary>
		/// Forgets a file for http challenges.
		/// </summary>
		/// <param name="domain">Domain where the file belongs</param>
		/// <param name="fileName">File name</param>
		private static void ClearHttpChallenge(string domain, string fileName)
		{
			ConcurrentDictionary<string, string> files = httpChallenges.GetOrAdd(domain, d => { return new ConcurrentDictionary<string, string>(); });
			files.TryRemove(fileName, out string ignored);
		}
		/// <summary>
		/// Returns the string that is to be the challenge response, or null if we don't know.  Can throw an exception if the request is invalid.
		/// </summary>
		/// <param name="host">Hostname requested by the client.</param>
		/// <param name="entrypoint">Entrypoint</param>
		/// <param name="exitpoint">Exitpoint</param>
		/// <returns></returns>
		public static string GetHttpChallengeResponse(string host, string fileName, Entrypoint entrypoint, Exitpoint exitpoint)
		{
			string[] domains = ValidateRequest(entrypoint, exitpoint);

			host = domains.FirstOrDefault(d => d.IEquals(host));

			if (host != null && httpChallenges.TryGetValue(host, out ConcurrentDictionary<string, string> files))
			{
				if (files.TryGetValue(fileName, out string payload))
					return payload;
			}
			return null;
		}
		#endregion
		#region TLS-ALPN-01 Challenge
		private static ConcurrentDictionary<string, X509Certificate> alpnCerts = new ConcurrentDictionary<string, X509Certificate>();

		private static void SetupTlsAlpn01Challenge(string token, string domain, IKey alpnCertKey)
		{
			string pem = acme.AccountKey.TlsAlpnCertificate(token, domain, alpnCertKey);
			CertificateChain cert = new CertificateChain(pem);
			Certes.Pkcs.PfxBuilder pfxBuilder = cert.ToPfx(alpnCertKey);
			byte[] pfx = pfxBuilder.Build(domain, "");
			alpnCerts[domain] = new X509Certificate2(pfx);
		}
		private static void ClearTlsAlpn01Challenge(string domain)
		{
			alpnCerts.TryRemove(domain, out X509Certificate ignored);
		}
		/// <summary>
		/// Returns the certificate needed for TLS-ALPN-01 validation for the given server name.
		/// </summary>
		/// <param name="serverName">Server Name (hostname) from the client.</param>
		/// <returns></returns>
		public static X509Certificate GetAcmeTls1Certificate(string serverName)
		{
			if (alpnCerts.TryGetValue(serverName, out X509Certificate cert))
				return cert;
			return null;
		}
		#endregion
		#region CertCache
		private static ConcurrentDictionary<string, CachedObject<X509Certificate2>> certCache = new ConcurrentDictionary<string, CachedObject<X509Certificate2>>();
		private static CachedObject<X509Certificate2> GetCertCache(string host)
		{
			string fileName = DomainToFileName(host) + ".pfx";
			if (!certCache.TryGetValue(fileName, out CachedObject<X509Certificate2> cache))
			{
				certCache[fileName] = cache = new CachedObject<X509Certificate2>(() =>
				{
					byte[] certData = Robust.Retry<byte[]>(() =>
					{
						if (File.Exists(CertsBaseDir + fileName))
							return File.ReadAllBytes(CertsBaseDir + fileName);
						return new byte[0];
					}
						 , ba => ba != null
						 , 50, 100, 200, 400, 800);
					if (certData == null || certData.Length == 0)
						return null;
					else
						return new X509Certificate2(certData);
				}
				, TimeSpan.FromMinutes(1)
				, TimeSpan.FromDays(1)
				, WebProxyService.ReportError);
			}
			return cache;
		}
		#endregion
		#region Helpers
		/// <summary>
		/// Gets the base directory path for the "Certs" directory, ending with '/'.
		/// </summary>
		public static string CertsBaseDir
		{
			get
			{
				return Globals.WritableDirectoryBase + "Certs/";
			}
		}
		/// <summary>
		/// Returns a lower case file name built from the given domain string.
		/// </summary>
		/// <param name="domain">domain name for the certificate file.</param>
		/// <returns></returns>
		public static string DomainToFileName(string domain)
		{
			string str = StringUtil.MakeSafeForFileName(domain).Trim().ToLower();
			if (string.IsNullOrWhiteSpace(str))
				str = "undefined";
			return str;
		}
		/// <summary>
		/// <para>Returns the default certificate path for the given host.</para>
		/// <para><c>Globals.WritableDirectoryBase + "Certs/" + DomainToFileName(host) + ".pfx"</c></para>
		/// </summary>
		/// <param name="host"></param>
		/// <returns></returns>
		public static string GetDefaultCertificatePath(string host)
		{
			return CertsBaseDir + DomainToFileName(host) + ".pfx";
		}

		/// <summary>
		/// Validates that the given entrypoint and exitpoint are eligible for automatic certificate management and returns the array of domain names.
		/// </summary>
		/// <param name="entrypoint">An entrypoint that is listening for HTTP on port 80 or HTTPS on port 443.</param>
		/// <param name="exitpoint">An exitpoint with [autoCertificate] enabled and one or more domains specified.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		private static string[] ValidateRequest(Entrypoint entrypoint, Exitpoint exitpoint)
		{
			if (entrypoint.httpPort != 80 && entrypoint.httpsPort != 443)
				throw new ArgumentException("CertMgr: entrypoint httpPort must be 80 or httpsPort must be 443.");

			if (exitpoint.type == ExitpointType.Disabled)
				throw new ArgumentException("CertMgr: exitpoint is currently disabled.");

			if (!exitpoint.autoCertificate)
				throw new ArgumentException("CertMgr: exitpoint does not have autoCertificate enabled.");

			string[] domains = exitpoint.getAllDomains();

			// Validate domains array
			if (domains == null || domains.Length == 0)
				throw new ArgumentException("CertMgr: 1 or more domains must be specified in exitpoint host field.");

			foreach (string domain in domains)
			{
				if (string.IsNullOrWhiteSpace(domain))
					throw new ArgumentException("CertMgr: domain is not allowed to be null or whitespace");
				if (IPAddress.TryParse(domain, out IPAddress ignored))
					throw new ArgumentException("CertMgr: domain is not allowed to be an IP address");
				if (domain.IEquals("localhost") || domain.IEndsWith(".localhost"))
					throw new ArgumentException("CertMgr: domain is not allowed to be localhost");
				if (domain.Contains("*"))
					throw new ArgumentException("CertMgr: domain is not allowed to contain wildcards");
			}

			return domains;
		}
		private static void SetLastError(Exception ex)
		{
			SetLastError(ex.ToHierarchicalString());
		}
		private static void SetLastError(string str)
		{
			if (string.IsNullOrEmpty(str))
			{
				LastError = null;
				Logger.Info("CertMgr.LastError Cleared");
			}
			else
			{
				LastError = DateTime.Now.ToString() + Environment.NewLine + str;
				Logger.Info("CertMgr.LastError = " + str);
			}
		}
		#endregion
	}
	public class AcmeException : Exception
	{
		public AcmeException(AcmeError error) : base(createMessage(error))
		{
		}

		private static string createMessage(AcmeError error)
		{
			string msg = error.Detail + Environment.NewLine
				+ "Identifier: " + (error.Identifier?.Value ?? "null") + Environment.NewLine
				+ "HTTP Status " + (int)error.Status + Environment.NewLine;
			if (error.Subproblems != null)
			{
				foreach (AcmeError sub in error.Subproblems)
				{
					msg += StringUtil.Indent(createMessage(sub), "  ");
				}
			}
			return msg;
		}
	}
}

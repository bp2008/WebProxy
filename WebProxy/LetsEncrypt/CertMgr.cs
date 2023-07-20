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
		private static Uri acmeServerUri = WellKnownServers.LetsEncryptStagingV2;
		private static async void Initialize(string email)
		{
			if (acme != null)
				return;

			await myLock.WaitAsync();
			try
			{
				if (acme != null)
					return;
				Settings settings = WebProxyService.MakeLocalSettingsReference();
				if (string.IsNullOrWhiteSpace(settings.acmeAccountKey))
				{
					Logger.Info("CertMgr.Initialize: Create LetsEncrypt Staging Account (" + email + ")");
					acme = new AcmeContext(acmeServerUri);
					IAccountContext account = await acme.NewAccount(email, true);

					// Save the account key for later use
					settings = WebProxyService.CloneSettingsObjectSlow();
					settings.acmeAccountEmail = email;
					settings.acmeAccountKey = acme.AccountKey.ToPem();
					WebProxyService.SaveNewSettings(settings);
				}
				else
				{
					Logger.Info("CertMgr.Initialize: Use existing LetsEncrypt Staging Account (" + email + ")");
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
		/// Returns the certificate for this connection or throws an exception if unable.
		/// </summary>
		/// <param name="letsEncryptAccountEmail">LetsEncrypt account email address, used only during account creation.</param>
		/// <param name="host">Hostname requested by the client.</param>
		/// <param name="entrypoint">Entrypoint</param>
		/// <param name="exitpoint">Exitpoint</param>
		/// <returns></returns>
		public static async Task<X509Certificate> GetCertificate(string letsEncryptAccountEmail, string host, Entrypoint entrypoint, Exitpoint exitpoint)
		{
			if (string.IsNullOrWhiteSpace(letsEncryptAccountEmail))
			{
				SetLastError("ACME Account Email has not been specified. Unable to generate certificate automatically.");
				return null;
			}
			Initialize(letsEncryptAccountEmail);

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
					await CreateCertificateForDomains(entrypoint.httpPort == 80, entrypoint.httpsPort == 443, domains);
				return cache.Reload();
			}
			else
			{
				if (shouldRenew)
				{
					// Create new certificate in background
					Logger.Info("CertMgr: Renew async " + host + " (" + string.Join(" ", domains) + ")");
					_ = CreateCertificateForDomains(entrypoint.httpPort == 80, entrypoint.httpsPort == 443, domains);
				}
				Logger.Info("CertMgr: Return cert " + host);
				return cert;
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
		/// <param name="standardHttpSupported">If true, the server is expected to be listening on standard HTTP port 80.  HTTP-01 validation is supported.</param>
		/// <param name="standardHttpsSupported">If true, the server is expected to be listening on standard HTTPS port 443.  TLS-ALPN-01 validation is supported.</param>
		/// <param name="domains">Domains to include in the certificate.</param>
		/// <returns></returns>
		private static async Task<bool> CreateCertificateForDomains(bool standardHttpSupported, bool standardHttpsSupported, string[] domains)
		{
			try
			{
				if (!standardHttpSupported && !standardHttpsSupported)
					throw new ArgumentException("standardHttpSupported and standardHttpsSupported are both false. Certificate validation is not possible.");

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
								}
								finally
								{
									ClearHttpChallenge(domain, challengeContext.Token);
								}
							}
						}
						else if (standardHttpsSupported)
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
								}
								finally
								{
									ClearTlsAlpn01Challenge(domain);
								}
							}
						}
						else
							throw new Exception("CertMgr.Create: The given entrypoint does not listen on default http (80) or https (443) ports, and cannot use automatic certificate management.");
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
						DirectoryInfo diCerts = new DirectoryInfo(Globals.ApplicationDirectoryBase + "../../StagingCerts");
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
					string certsBaseDir = Globals.WritableDirectoryBase + "Certs/";
					Robust.RetryPeriodic(() =>
					{
						Directory.CreateDirectory(certsBaseDir);
					}, 50, 100);

					foreach (string domain in domains)
					{
						Robust.RetryPeriodic(() =>
						{
							File.WriteAllBytes(certsBaseDir + DomainToFileName(domain) + ".pfx", pfx);
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
		private static Dictionary<string, CachedObject<X509Certificate2>> certCache = new Dictionary<string, CachedObject<X509Certificate2>>();
		private static CachedObject<X509Certificate2> GetCertCache(string host)
		{
			string certsBaseDir = Globals.WritableDirectoryBase + "Certs/";
			string fileName = DomainToFileName(host) + ".pfx";
			if (!certCache.TryGetValue(fileName, out CachedObject<X509Certificate2> cache))
			{
				certCache[fileName] = cache = new CachedObject<X509Certificate2>(() =>
				{
					byte[] certData = Robust.Retry<byte[]>(() =>
					{
						if (File.Exists(certsBaseDir + fileName))
							return File.ReadAllBytes(certsBaseDir + fileName);
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
		/// Returns a lower case file name built from the given domain string.
		/// </summary>
		/// <param name="domain"></param>
		/// <returns></returns>
		private static string DomainToFileName(string domain)
		{
			return StringUtil.MakeSafeForFileName(domain).ToLower();
		}

		private static string[] ValidateRequest(Entrypoint entrypoint, Exitpoint exitpoint)
		{
			if (entrypoint.httpPort != 80 && entrypoint.httpsPort != 443)
				throw new ArgumentException("CertMgr: entrypoint httpPort must be 80 or httpsPort must be 443.");

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

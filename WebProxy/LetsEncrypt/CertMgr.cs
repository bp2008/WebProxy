using BPUtil;
using Certify.ACME.Anvil;
using Certify.ACME.Anvil.Acme;
using Certify.ACME.Anvil.Acme.Resource;
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
using WebProxy.Utility;
using Authorization = Certify.ACME.Anvil.Acme.Resource.Authorization;
using Directory = System.IO.Directory;

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
		private static async Task Initialize()
		{
			Settings settings = WebProxyService.MakeLocalSettingsReference();
			if (string.IsNullOrWhiteSpace(settings.acmeAccountEmail))
				throw new ApplicationException("LetsEncrypt Account Email has not been specified. Unable to use automatic certificate management.");

			if (acme != null && LastEmail == settings.acmeAccountEmail)
				return;

			await myLock.WaitAsync().ConfigureAwait(false);
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
					IAccountContext account = await acme.NewAccount(settings.acmeAccountEmail, true).ConfigureAwait(false);

					// Save the account key for later use
					settings = WebProxyService.CloneSettingsObjectSlow();
					settings.acmeAccountKey = acme.AccountKey.ToPem();
					await WebProxyService.SaveNewSettings(settings).ConfigureAwait(false);
				}
				else
				{
					Logger.Info("CertMgr.Initialize: Use existing LetsEncrypt" + (acmeServerUri == WellKnownServers.LetsEncryptStagingV2 ? " Staging" : "") + " Account (" + settings.acmeAccountEmail + ")");
					IKey accountKey = KeyFactory.FromPem(settings.acmeAccountKey);
					acme = new AcmeContext(acmeServerUri, accountKey);
					IAccountContext accountContext = await acme.Account().ConfigureAwait(false);
					Account account = await accountContext.Resource().ConfigureAwait(false);
					string emailContact = "mailto:" + settings.acmeAccountEmail;
					if (account.Contact == null || account.Contact.Count < 1 || account.Contact[0] != emailContact)
					{
						string oldContactStr = account.Contact == null ? "null" : string.Join(",", account.Contact);
						List<string> newContact = new List<string>(new string[] { emailContact });
						Logger.Info("CertMgr.Initialize: Changing LetsEncrypt Account Contact from \"" + oldContactStr + "\" to \"" + string.Join(",", newContact) + "\"");
						await accountContext.Update(newContact, true).ConfigureAwait(false);
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
		/// <param name="createNew">If true, this operation will be allowed to create the certificate if it does not already exist.  If false and the certificate does not exist, the task result will be a null certificate.</param>
		/// <returns></returns>
		public static async Task<X509Certificate> GetCertificate(string host, Entrypoint entrypoint, Exitpoint exitpoint, bool createNew)
		{
			string[] domains = ValidateRequestOrThrow(entrypoint, exitpoint);

			host = exitpoint.getHostnameMatch(host);

			if (string.IsNullOrWhiteSpace(host) || host.Contains('/') || host.Contains('\\'))
				throw new ArgumentException("host is invalid");

			if (string.IsNullOrWhiteSpace(exitpoint.certificatePath))
			{
				Settings newSettings = WebProxyService.CloneSettingsObjectSlow();
				exitpoint = newSettings.exitpoints.First(e => e.name == exitpoint.name);
				exitpoint.certificatePath = CertMgr.GetDefaultCertificatePath(domains.First());
				await WebProxyService.SaveNewSettings(newSettings).ConfigureAwait(false);
			}

			// Try to get existing certificate.
			CachedObject<X509Certificate2> cache = GetCertCache(host);

			bool shouldRenew = ShouldRenew(cache, domains, createNew);
			// ShouldRenew forces a reload of the cache if there's no cert, so it should be called before we try to get the cached instance.
			X509Certificate2 cert = cache.GetInstance();
			if (cert == null)
			{
				if (shouldRenew)
					Logger.Info("CertMgr: Create now " + host + " (" + string.Join(" ", domains) + ")");
				// Create new certificate
				if (shouldRenew)
					await CreateCertificateForDomains(entrypoint, exitpoint).ConfigureAwait(false);
				return cache.Reload();
			}
			else
			{
				if (shouldRenew)
				{
					// Create new certificate in background
					Logger.Info("CertMgr: Renew async " + host + " (" + string.Join(" ", domains) + ")");
					_ = CreateCertificateForDomains(entrypoint, exitpoint).ConfigureAwait(false);
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
				string[] domains = ValidateRequestOrThrow(entrypoint, exitpoint);

				lock (renewCheckLock)
					WebProxyService.certRenewalDates.StartCooldown(domains);

				Logger.Info("CertMgr: Force renew " + string.Join(" ", domains));

				bool success = await CreateCertificateForDomains(entrypoint, exitpoint).ConfigureAwait(false);
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
		/// <summary>
		/// Returns true if now is a good time for this thread to renew the certificate.
		/// If this method returns true, it is important that a renewal is attempted because there's a cooldown before the method will return true again.
		/// </summary>
		/// <param name="cache">CachedObject responsible for loading the certificate from disk.</param>
		/// <param name="domains">The array of domains belonging to this certificate.</param>
		/// <param name="createNew">True if this operation is allowed to create a certificate that is missing.</param>
		/// <returns></returns>
		private static bool ShouldRenew(CachedObject<X509Certificate2> cache, string[] domains, bool createNew)
		{
			X509Certificate2 cert = cache.GetInstance();
			if (cert == null)
				cert = cache.Reload();

			TimeSpan cooldownTime;
			if (cert == null)
			{
				if (!createNew)
					return false;
				cooldownTime = TimeSpan.FromHours(4); // Retry every 4 hours until certificate is generated.
			}
			else
			{
				DateTime expDate = DateTime.Parse(cert.GetExpirationDateString());
				if (expDate <= DateTime.Now.AddDays(1))
					cooldownTime = TimeSpan.FromHours(4); // Expiring within 1 day. Attempt to renew every 4 hours.
				else if (expDate <= DateTime.Now.AddMonths(1))
					cooldownTime = TimeSpan.FromDays(1); // Expiring within 1 month. Attempt to renew every day.
				else
					return false; // No need to renew
			}
			lock (renewCheckLock)
			{
				if (WebProxyService.certRenewalDates.AnyOffCooldown(domains, (long)cooldownTime.TotalMilliseconds))
				{
					WebProxyService.certRenewalDates.StartCooldown(domains);
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

				if (!standardHttpSupported && !standardHttpsSupported && !dns01Cloudflare)
					throw new ArgumentException("Certificate creation will not be attempted because no ACME validation method is currently possible. Ensure that this server is listening on public port 80 (http), 443 (https), or that DNS validation is properly configured.");

				await Initialize().ConfigureAwait(false);


				await myLock.WaitAsync().ConfigureAwait(false);
				try
				{
					// Create order for certificate.
					Logger.Info("CertMgr.Create: order");
					IOrderContext orderContext = await acme.NewOrder(domains).ConfigureAwait(false);
					Order order = await orderContext.Resource().ConfigureAwait(false);
					IEnumerable<IAuthorizationContext> authorizations = await orderContext.Authorizations().ConfigureAwait(false);
					IKey alpnCertKey = null;
					foreach (IAuthorizationContext authz in authorizations)
					{
						Authorization authResource = await authz.Resource().ConfigureAwait(false);
						string domain = authResource.Identifier.Value;
						if (authResource.Status == AuthorizationStatus.Valid)
						{
							Logger.Info("CertMgr.Create: " + domain + " is already validated with expiration: " + authResource.Expires);
							continue;
						}
						IEnumerable<IChallengeContext> allChallenges = await authz.Challenges().ConfigureAwait(false);
						Logger.Info("CertMgr.Create: " + domain + " can be validated via: " + string.Join(", ", allChallenges.Select(cc => cc.Type)));

						// Validate domain ownership.
						if (dns01Cloudflare)
						{
							IChallengeContext challengeContext = await authz.Dns().ConfigureAwait(false);
							if (challengeContext != null)
							{
								Logger.Info("CertMgr.Create: " + domain + " starting validation via " + challengeContext.Type);
								string dnsKey = "_acme-challenge." + domain;
								string dnsValue = acme.AccountKey.DnsTxt(challengeContext.Token);
								try
								{
									await CloudflareDnsValidator.CreateDNSRecord(dnsKey, dnsValue).ConfigureAwait(false);
									Logger.Info("CertMgr.Create Cloudflare-DNS-01: " + dnsKey + " TXT record created with value: \"" + dnsValue + "\". Waiting up to 1 minute or until TXT query succeeds.");
									// 2024-11-03 - One of my certificates expired because a fixed 5 second wait time between creation and validation is simply not long enough to be reliable anymore.
									// So, now we will wait up to one minute until our own DNS query succeeds and THEN wait an additional 5 seconds.
									string result = await DnsLookupHelper.GetTXTRecord(dnsKey, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
									if (result == dnsValue)
										Logger.Info("CertMgr.Create TXT query received the expected result.");
									else if (result == null)
										Logger.Info("CertMgr.Create TXT query was unable to retrieve a result.");
									else
										Logger.Info("CertMgr.Create TXT query retrieved unexpected result: \"" + result + "\".");
									Logger.Info("CertMgr.Create Waiting an additional 5 seconds so the validation is less likely to fail.");
									await Task.Delay(5000).ConfigureAwait(false);

									Challenge challenge = await ValidateAndWait(challengeContext).ConfigureAwait(false);
									continue;
								}
								finally
								{
									try
									{
										await CloudflareDnsValidator.DeleteDNSRecord(dnsKey).ConfigureAwait(false);
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
							IChallengeContext challengeContext = await authz.Http().ConfigureAwait(false);
							if (challengeContext != null)
							{
								Logger.Info("CertMgr.Create: " + domain + " starting validation via " + challengeContext.Type);
								try
								{
									SetupHttpChallenge(domain, challengeContext.Token, challengeContext.KeyAuthz);

									Challenge challenge = await ValidateAndWait(challengeContext).ConfigureAwait(false);
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
							IChallengeContext challengeContext = await authz.TlsAlpn().ConfigureAwait(false);
							if (challengeContext != null)
							{
								if (alpnCertKey == null)
									alpnCertKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
								Logger.Info("CertMgr.Create: " + domain + " starting validation via " + challengeContext.Type);
								try
								{
									SetupTlsAlpn01Challenge(challengeContext.Token, domain, alpnCertKey);

									Challenge challenge = await ValidateAndWait(challengeContext).ConfigureAwait(false);
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
					CertificateChain cert = await orderContext.Generate(csrInfo, privateKey).ConfigureAwait(false);
					Certify.ACME.Anvil.Pkcs.PfxBuilder pfxBuilder = cert.ToPfx(privateKey);
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

					try
					{
						BPUtil.SimpleHttp.TLS.CertificateStoreUpdater.EnsureIntermediateCertificatesAreInStore(pfx, null);
					}
					catch (Exception ex)
					{
						Logger.Debug(ex, "Unable to save intermediate certificates to \"Intermediate Certificate Authorites\" store.");
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
		private static async Task<Challenge> ValidateAndWait(IChallengeContext challenge)
		{
			Challenge result = await challenge.Validate().ConfigureAwait(false);
			CountdownStopwatch sw = CountdownStopwatch.StartNew(TimeSpan.FromMinutes(1));
			while (!sw.Finished && result.Status == ChallengeStatus.Pending || result.Status == ChallengeStatus.Processing)
			{
				result = await challenge.Resource().ConfigureAwait(false);
				await Task.Delay(500).ConfigureAwait(false);
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
			string[] domains = ValidateRequestOrThrow(entrypoint, exitpoint);

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
			Certify.ACME.Anvil.Pkcs.PfxBuilder pfxBuilder = cert.ToPfx(alpnCertKey);
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
			if (domain == "*")
				return "wildcard_root";
			if (domain.Contains("*"))
				domain = domain.Replace("*", "wildcard");
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
		/// Validates that the given entrypoint and exitpoint are eligible for automatic certificate management and returns the array of domain names. Throws if validation fails.
		/// </summary>
		/// <param name="entrypoint">An entrypoint that has the exitpoint routed to it.</param>
		/// <param name="exitpoint">An exitpoint with [autoCertificate] enabled and one or more domains specified.</param>
		/// <returns>The array of domain names</returns>
		/// <exception cref="ArgumentException">Throws if validation fails.</exception>
		private static string[] ValidateRequestOrThrow(Entrypoint entrypoint, Exitpoint exitpoint)
		{
			string err = ValidateRequest(entrypoint, exitpoint, out string[] domains);
			if (err == null)
				return domains;
			else
				throw new ArgumentException("CertMgr: " + err);
		}
		/// <summary>
		/// Validates that the given entrypoint and exitpoint are eligible for automatic certificate management and returns an error message or null.
		/// </summary>
		/// <param name="entrypoint">An entrypoint that has the exitpoint routed to it.</param>
		/// <param name="exitpoint">An exitpoint with [autoCertificate] enabled and one or more domains specified.</param>
		/// <param name="domains">(Output) The array of domain names from <see cref="Exitpoint.host"/>.</param>
		/// <returns>An error message or null.</returns>
		public static string ValidateRequest(Entrypoint entrypoint, Exitpoint exitpoint, out string[] domains)
		{
			domains = null;

			if (entrypoint == null)
				return "Entrypoint was not provided.";

			Settings settings = WebProxyService.MakeLocalSettingsReference();
			bool dnsValidationConfigured = !string.IsNullOrWhiteSpace(settings.cloudflareApiToken) && exitpoint.cloudflareDnsValidation;
			if (!dnsValidationConfigured)
			{
				if (entrypoint.httpPort != 80 && entrypoint.httpsPort != 443)
					return "entrypoint httpPort must be 80 or httpsPort must be 443 or the exitpoint must have DNS validation configured.";
			}

			if (exitpoint.type == ExitpointType.Disabled)
				return "exitpoint is currently disabled.";

			if (!exitpoint.autoCertificate)
				return "exitpoint does not have autoCertificate enabled.";

			domains = exitpoint.getAllDomains();

			// Validate domains array
			if (domains == null || domains.Length == 0)
				return "1 or more domains must be specified in exitpoint host field.";

			foreach (string domain in domains)
			{
				if (string.IsNullOrWhiteSpace(domain))
					return "domain is not allowed to be null or whitespace when using LetsEncrypt";
				if (IPAddress.TryParse(domain, out IPAddress ignored))
					return "domain is not allowed to be an IP address when using LetsEncrypt";
				if (domain.IEquals("localhost") || domain.IEndsWith(".localhost"))
					return "domain is not allowed to be localhost when using LetsEncrypt";
				if (domain == "*")
					return "Wildcard root domain is not allowed when using LetsEncrypt";
				if (!dnsValidationConfigured && domain.Contains("*"))
					return "domain is not allowed to contain wildcards when using LetsEncrypt without DNS validation";
			}

			return null;
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

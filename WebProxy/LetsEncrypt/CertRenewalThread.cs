using BPUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy.LetsEncrypt
{
	/// <summary>
	/// Manages a foreground thread that performs certificate renewals.
	/// </summary>
	public static class CertRenewalThread
	{
		static volatile bool abort = false;
		static Thread thrRenewals;

		public static void Start()
		{
			if (thrRenewals != null)
				throw new Exception("CertRenewalThread is already running.");
			thrRenewals = new Thread(renewalWork);
			thrRenewals.Name = "Certificate Renewals";
			thrRenewals.IsBackground = false;
			thrRenewals.Start();
		}

		/// <summary>
		/// Stops the Certificate Renewal Thread.
		/// </summary>
		public static void Stop()
		{
			if (abort)
				throw new Exception("CertRenewalThread is already stopped.");
			abort = true;
			try
			{
				thrRenewals?.Join();
			}
			catch { }
		}

		private static void renewalWork(object obj)
		{
			try
			{
				IntervalSleeper sleeper = new IntervalSleeper(250);
				sleeper.SleepUntil((long)TimeSpan.FromMinutes(5).TotalMilliseconds, () => abort);
				while (!abort)
				{
					if (abort)
						return;
					try
					{
						Settings settings = WebProxyService.MakeLocalSettingsReference();
						foreach (Exitpoint exitpoint in settings.exitpoints)
						{
							// Get the best entrypoint for ACME validation.
							IEnumerable<Entrypoint> entrypoints = settings.proxyRoutes
								.Where(pr => pr.exitpointName == exitpoint.name)
								.Select(pr => pr.entrypointName)
								.Select(entrypointName => settings.entrypoints.FirstOrDefault(e => e.name == entrypointName))
								.Where(e => e != null);

							Entrypoint entrypoint = entrypoints.FirstOrDefault(e => e.httpPort == 80 || e.httpsPort == 443);
							if (entrypoint == null)
								entrypoint = entrypoints.FirstOrDefault();
							if (entrypoint == null)
								continue; // If there is no entrypoint mapped to this exitpoint, then there is no need to renew its certificate.

							string err = CertMgr.ValidateRequest(entrypoint, exitpoint, out string[] domains);
							if (err != null)
								continue;

							foreach (string domain in domains)
							{
								try
								{
									CertMgr.GetCertificate(domain, entrypoint, exitpoint, false).Wait();
								}
								catch (Exception ex)
								{
									Logger.Debug(ex);
								}

								if (abort)
									return;
							}
						}
					}
					catch (Exception ex)
					{
						Logger.Debug(ex);
					}
					sleeper.SleepUntil((long)TimeSpan.FromHours(4).TotalMilliseconds, () => abort);
				}
			}
			finally
			{
				thrRenewals = null;
			}
		}
	}
}

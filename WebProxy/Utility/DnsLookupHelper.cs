using BPUtil;
using DnsClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy.Utility
{
	/// <summary>
	/// Offers DNS lookup methods.
	/// </summary>
	public static class DnsLookupHelper
	{
		/// <summary>
		/// <para>Queries DNS for a TXT record repeatedly until it returns successfully or a timeout is reached.  Returns the string content of the first delivered TXT record if successful, otherwise returns null.</para>
		/// <para>There is a waiting period of 1 second between DNS queries.</para>
		/// </summary>
		/// <param name="name">DNS record name to query.</param>
		/// <param name="timeout">Timeout after which this method should return null if the DNS query has not been successful yet.</param>
		/// <returns>The string content of the first delivered TXT record, or null if none was delivered before the timeout.</returns>
		public static async Task<string> GetTXTRecord(string name, TimeSpan timeout)
		{
			LookupClient lookupClient = new LookupClient();
			DnsQuestion dnsQ = new DnsQuestion(name, QueryType.TXT);
			DnsQueryAndServerOptions options = new DnsQueryAndServerOptions(NameServer.Cloudflare);

			int fails = 0;
			CountdownStopwatch countdownStopwatch = CountdownStopwatch.StartNew(timeout);
			using (CancellationTokenSource ctsTimeout = new CancellationTokenSource(timeout))
			{
				while (!countdownStopwatch.Finished)
				{
					if (countdownStopwatch.RemainingMilliseconds < 1100)
						return null;
					if (fails > 0)
					{
						await Task.Delay(1000).ConfigureAwait(false);
						if (countdownStopwatch.RemainingMilliseconds < 50)
							return null;
					}
					try
					{
						IDnsQueryResponse result = await lookupClient.QueryAsync(dnsQ, options, ctsTimeout.Token).ConfigureAwait(false);
						string txt = result.Answers.TxtRecords().FirstOrDefault()?.Text?.FirstOrDefault();
						if (txt != null)
							return txt;
					}
					catch (OperationCanceledException)
					{
						return null;
					}
					catch (DnsResponseException)
					{
					}
					fails++;
				}
			}
			return null;
		}
	}
}

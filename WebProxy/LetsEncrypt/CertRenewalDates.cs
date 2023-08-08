using BPUtil;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	/// <summary>
	/// Provides a concurrent dictionary of domain to last certificate renewal date.
	/// </summary>
	public class CertRenewalDates : SerializableObjectJson
	{
		/// <summary>
		/// A dictionary of the latest certificate renewal dates (unix epoch milliseconds) keyed by domain.
		/// </summary>
		[JsonProperty("certificateRenewalDatesPerDomain")]
		private ConcurrentDictionary<string, long> certificateRenewalDatesPerDomain = new ConcurrentDictionary<string, long>();

		protected override object DeserializeFromJson(string str)
		{
			return Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(str);
		}

		protected override string SerializeToJson(object obj)
		{
			return Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
		}
		/// <summary>
		/// Returns true if any of the given domains are off cooldown.
		/// </summary>
		/// <param name="domains">Array of domains.</param>
		/// <param name="cooldownMs">Cooldown time in milliseconds.</param>
		/// <returns></returns>
		public bool AnyOffCooldown(string[] domains, long cooldownMs)
		{
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			foreach (string domain in domains)
			{
				if (certificateRenewalDatesPerDomain.TryGetValue(domain, out long lastTime))
				{
					long timePassedMs = now - lastTime;
					if (timePassedMs > cooldownMs)
						return true;
				}
				else
					return true;
			}
			return false;
		}
		/// <summary>
		/// Starts a new cooldown for all of the given domains and saves the cooldowns to the file.
		/// </summary>
		/// <param name="domains">Array of domains.</param>
		public void StartCooldown(string[] domains)
		{
			long now = TimeUtil.GetTimeInMsSinceEpoch();
			foreach (string domain in domains)
				certificateRenewalDatesPerDomain[domain] = now;
			Save();
		}
	}
}
using BPUtil;
using FluentCloudflare.Abstractions.Builders;
using FluentCloudflare.Abstractions.Builders.Dns;
using FluentCloudflare.Api;
using FluentCloudflare.Api.Entities;
using FluentCloudflare.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebProxy.LetsEncrypt
{
	/// <summary>
	/// Static class that configures Cloudflare DNS to complete ACME DNS-01 challenges.
	/// </summary>
	public static class CloudflareDnsValidator
	{
		private static readonly HttpClient httpClient = new HttpClient();
		private static IAuthorizedSyntax GetCloudflareContext()
		{
			string key = WebProxyService.MakeLocalSettingsReference().cloudflareApiToken;
			if (key == null)
				throw new Exception("The Cloudflare API Token was not found in settings.");
			return FluentCloudflare.Cloudflare.WithToken(key);
		}
		private static async Task<Zone> GetZone(IAuthorizedSyntax context, string recordName)
		{
			List<Zone> allZones = await GetAllZones(context).ConfigureAwait(false);
			if (allZones.Count == 0)
				throw new Exception("CloudflareDnsValidator: Cloudflare API did not return any DNS zones.");
			Zone best = allZones
				.Select(zone =>
				{
					// There can be zones defined for subdomains.  This selector helps determine which zone matches the most subdomains.
					string nameTrimmed = zone.Name.TrimEnd('.');
					int matchQuality = (recordName.IEquals(nameTrimmed) || recordName.IEndsWith("." + nameTrimmed)) ? nameTrimmed.Split('.').Length : 0;
					return new { zone, matchQuality };
				})
				.Where(o => o.matchQuality > 0)
				.OrderByDescending(o => o.matchQuality)
				.FirstOrDefault()?.zone;
			if (best == null)
				throw new Exception("CloudflareDnsValidator: DNS zone " + recordName + " could not be found.");
			return best;
		}

		private static async Task<List<Zone>> GetAllZones(IAuthorizedSyntax context)
		{
			List<Zone> allZones = new List<Zone>();
			int page = 0;
			int zoneCount = int.MaxValue;
			while (allZones.Count < zoneCount)
			{
				page++;
				Response<List<Zone>> zonesResp = await context.Zones.List().PerPage(50).Page(page).ParseAsync(httpClient).ConfigureAwait(false);
				if (!zonesResp.Success || zonesResp.ResultInfo.Count == 0)
					break;
				zoneCount = zonesResp.ResultInfo.TotalCount;
				allZones.AddRange(zonesResp.Unpack());
			}

			return allZones;
		}
		/// <summary>
		/// Creates the specified TXT record in Cloudflare DNS.
		/// </summary>
		/// <param name="domain">Domain string, e.g. "_acme-challenge.www.example.com".</param>
		/// <param name="value">Value string, e.g. "abc123".</param>
		/// <returns></returns>
		public static async Task CreateDNSRecord(string domain, string value)
		{
			IAuthorizedSyntax context = GetCloudflareContext();
			Zone zone = await GetZone(context, domain).ConfigureAwait(false);

			IDnsSyntax dns = context.Zone(zone).Dns;
			_ = await dns.Create(DnsRecordType.TXT, domain, value)
				.Ttl(60)
				.CallAsync(httpClient)
				.ConfigureAwait(false);
		}
		/// <summary>
		/// Deletes the specified TXT record in Cloudflare DNS, if it exists. Returns true if successful, false if the record did not exist.  Throws if the record could not be deleted due to an error.
		/// </summary>
		/// <param name="domain">Domain string, e.g. "_acme-challenge.www.example.com".</param>
		/// <param name="value">Value string, e.g. "abc123".</param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static async Task<bool> DeleteDNSRecord(string domain, string value)
		{
			IAuthorizedSyntax context = GetCloudflareContext();
			Zone zone = await GetZone(context, domain).ConfigureAwait(false);

			IDnsSyntax dns = context.Zone(zone).Dns;
			List<DnsRecord> records = await dns
				.List()
				.OfType(DnsRecordType.TXT)
				.WithName(domain)
				.WithContent(value)
				.Match(MatchType.All)
				.CallAsync(httpClient)
				.ConfigureAwait(false);
			DnsRecord record = records.FirstOrDefault();
			if (record == null)
				return false;

			try
			{
				_ = await dns.Delete(record.Id)
					.CallAsync(httpClient)
					.ConfigureAwait(false);
				return true;
			}
			catch (Exception ex)
			{
				throw new Exception("Unable to delete \"" + domain + "\" TXT record from Cloudflare.", ex);
			}
		}
		/// <summary>
		/// Deletes all TXT records with the given key in Cloudflare DNS. Returns the number of records deleted.  Throws if an error occurred.
		/// </summary>
		/// <param name="domain">Domain string, e.g. "_acme-challenge.www.example.com".</param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static async Task<int> DeleteDNSRecord(string domain)
		{
			IAuthorizedSyntax context = GetCloudflareContext();
			Zone zone = await GetZone(context, domain).ConfigureAwait(false);

			IDnsSyntax dns = context.Zone(zone).Dns;
			List<DnsRecord> records = await dns
				.List()
				.OfType(DnsRecordType.TXT)
				.WithName(domain)
				.Match(MatchType.All)
				.CallAsync(httpClient)
				.ConfigureAwait(false);

			int recordsDeleted = 0;
			foreach (DnsRecord record in records)
			{
				try
				{
					_ = await dns.Delete(record.Id)
						.CallAsync(httpClient)
						.ConfigureAwait(false);
					recordsDeleted++;
				}
				catch (Exception ex)
				{
					throw new Exception("Unable to delete \"" + domain + "\" TXT record from Cloudflare.", ex);
				}
			}
			return recordsDeleted;
		}
		/// <summary>
		/// Returns the first zone name in alphabetical order that has at least one nameserver configured. E.g. "example.com".
		/// </summary>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static async Task<string> GetAnyConfiguredDomain()
		{
			IAuthorizedSyntax context = GetCloudflareContext();
			List<Zone> allZones = await GetAllZones(context).ConfigureAwait(false);
			if (allZones.Count == 0)
				throw new Exception("CloudflareDnsValidator: Cloudflare API did not return any DNS zones.");
			Zone best = allZones
				.OrderBy(z => z.Name)
				.FirstOrDefault(z => z.NameServers.Count > 0);
			if (best == null)
				throw new Exception("CloudflareDnsValidator: Unable to find any configured domain names in your Cloudflare account.");
			return best.Name;
		}
	}
}
using BPUtil.SimpleHttp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using WebProxy;

namespace WebProxyTests
{
	[TestClass]
	public class TestEntrypoint
	{
		[TestMethod]
		public void Test_isHostnameMatch()
		{
			bool isExactMatch;
			Entrypoint e = new Entrypoint();

			e.ipAddress = null;

			Assert.IsTrue(e.isIpMatch(IPAddress.Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.IPv6Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.Loopback, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.Parse("1.1.1.1"), out isExactMatch));
			Assert.IsFalse(isExactMatch);

			e.ipAddress = "";

			Assert.IsTrue(e.isIpMatch(IPAddress.Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.IPv6Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.Loopback, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.Parse("1.1.1.1"), out isExactMatch));
			Assert.IsFalse(isExactMatch);

			e.ipAddress = "1.1.1.1";

			Assert.IsFalse(e.isIpMatch(IPAddress.Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isIpMatch(IPAddress.IPv6Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isIpMatch(IPAddress.Loopback, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.Parse("1.1.1.1"), out isExactMatch));
			Assert.IsTrue(isExactMatch);

			e.ipAddress = IPAddress.Loopback.ToString();

			Assert.IsFalse(e.isIpMatch(IPAddress.Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isIpMatch(IPAddress.IPv6Any, out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isIpMatch(IPAddress.Loopback, out isExactMatch));
			Assert.IsTrue(isExactMatch);
			Assert.IsFalse(e.isIpMatch(IPAddress.Parse("1.1.1.1"), out isExactMatch));
			Assert.IsFalse(isExactMatch);
		}
	}
}

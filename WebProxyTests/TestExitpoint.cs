using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WebProxy;

namespace WebProxyTests
{
	[TestClass]
	public class TestExitpoint
	{
		[TestMethod]
		public void Test_isHostnameMatch()
		{
			bool isExactMatch;
			Exitpoint e = new Exitpoint();

			e.host = "www.example.com";

			Assert.IsTrue(e.isHostnameMatch("www.example.com", out isExactMatch));
			Assert.IsTrue(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("testexample.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example.org", out isExactMatch));
			Assert.IsFalse(isExactMatch);

			e.host = "*.example.com";

			Assert.IsTrue(e.isHostnameMatch("www.example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("testexample.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example.org", out isExactMatch));
			Assert.IsFalse(isExactMatch);

			e.host = "*example.com";

			Assert.IsTrue(e.isHostnameMatch("www.example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isHostnameMatch("example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isHostnameMatch("testexample.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example.org", out isExactMatch));
			Assert.IsFalse(isExactMatch);

			e.host = "example.*";

			Assert.IsFalse(e.isHostnameMatch("www.example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isHostnameMatch("example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("testexample.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example.org", out isExactMatch));
			Assert.IsFalse(isExactMatch);

			e.host = "*example.*";

			Assert.IsTrue(e.isHostnameMatch("www.example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isHostnameMatch("example.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsFalse(e.isHostnameMatch("www.example", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isHostnameMatch("testexample.com", out isExactMatch));
			Assert.IsFalse(isExactMatch);
			Assert.IsTrue(e.isHostnameMatch("www.example.org", out isExactMatch));
			Assert.IsFalse(isExactMatch);

			// Also test improper usage
			Assert.IsTrue(e.isHostnameMatch("*example.*", out isExactMatch));
			// It is impossible to have exact matches with wildcard queries, even if the host template does happen to exactly match what is entered (because * is not valid in an actual hostname).  DNS names can contain only alphabetic characters (a-z, A-Z), numeric characters (0-9), the minus sign (-), and the period (.)
			Assert.IsFalse(isExactMatch);
		}
	}
}

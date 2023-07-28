using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ReleaseArchiver
{
	class Program
	{
		static void Main(string[] args)
		{
			CreateRelease("net6.0", "WebProxy Linux", "WebProxyLinux.dll");
			CreateRelease("net6.0-windows", "WebProxy Windows", "WebProxy.dll");
			Console.WriteLine();
			Console.WriteLine("Press ENTER to exit.");
			Console.ReadLine();
		}

		private static void CreateRelease(string folder, string name, string fileForVersioning)
		{
			string releasePath = "../../../../WebProxy/bin/Release/" + folder;
			string dllPath = Path.Combine(releasePath, fileForVersioning);
			string version = AssemblyName.GetAssemblyName(dllPath).Version.ToString();
			string zipName = name + " " + version + ".zip";
			string releasesDir = "../../../../Releases";
			string zipPath = Path.Combine(releasesDir, zipName);

			if (File.Exists(zipPath))
				Console.WriteLine(zipName + " ALREADY EXISTS");
			else
			{
				Console.WriteLine("Creating " + zipName);
				if (!Directory.Exists(releasesDir))
					Directory.CreateDirectory(releasesDir);

				ZipFile.CreateFromDirectory(releasePath, zipPath);
			}
		}
	}
}
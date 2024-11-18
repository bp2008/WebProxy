using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;

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
			string version = "";
			try
			{
				string releasePath = "../../../../WebProxy/bin/Release/" + folder;
				string dllPath = Path.Combine(releasePath, fileForVersioning);
				version = AssemblyName.GetAssemblyName(dllPath).Version.ToString();
				string zipName = name + " " + version + ".zip";
				string releasesDir = "../../../../Releases";
				string zipPath = Path.Combine(releasesDir, zipName);

				if (File.Exists(zipPath))
					Console.WriteLine(zipName + " ALREADY EXISTS");
				else
				{
					DirectoryInfo diReleaseSourceDir = new DirectoryInfo(releasePath);
					FileInfo[] diReleaseSourceFiles = diReleaseSourceDir.GetFiles("*", SearchOption.AllDirectories);
					string[] releaseSourceFilePaths = diReleaseSourceFiles.Select(fi => fi.FullName.Replace('\\', '/')).ToArray();
					if (!releaseSourceFilePaths.Any(p => p.EndsWith("/www/index.html")))
						throw new Exception(folder + "/www/index.html not found.");
					if (!releaseSourceFilePaths.Any(p => Regex.IsMatch(p, "/www/assets/index-(.+)\\.js$")))
						throw new Exception(folder + "/www/assets/index-*.js not found.");
					if (!releaseSourceFilePaths.Any(p => Regex.IsMatch(p, "/www/assets/index-(.+)\\.css$")))
						throw new Exception(folder + "/www/assets/index-*.css not found.");

					Console.WriteLine("Creating " + zipName);
					if (!Directory.Exists(releasesDir))
						Directory.CreateDirectory(releasesDir);

					ZipFile.CreateFromDirectory(releasePath, zipPath);
				}
			}
			catch (Exception ex)
			{
				WriteError(ex.Message);
				WriteError("Release " + version + " not created properly.");
			}
		}

		private static void WriteError(string message)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine(message);
			Console.ResetColor();
		}
	}
}
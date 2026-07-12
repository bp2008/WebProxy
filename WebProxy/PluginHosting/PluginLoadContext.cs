using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace WebProxy
{
	/// <summary>
	/// <para>A collectible AssemblyLoadContext which loads one plugin package (one subdirectory of the "Plugins" directory).</para>
	/// <para>Assemblies are loaded from bytes (not from file paths) so that plugin files are never locked and can be replaced or deleted while the service is running.</para>
	/// <para>Assembly resolution rules:</para>
	/// <para>* Assemblies which are already loaded into the default load context (WebProxy itself, WebProxy.Plugins, BPUtil, framework and shared assemblies) are never loaded from the plugin directory; the host's copy is used so that types unify between the host and the plugin.</para>
	/// <para>* Other dependencies are loaded from the plugin's own directory if a correspondingly-named DLL exists there.</para>
	/// <para>* Anything else falls through to the default load context (e.g. framework assemblies which have not been loaded yet).</para>
	/// </summary>
	public class PluginLoadContext : AssemblyLoadContext
	{
		private readonly string pluginDirectory;
		/// <summary>
		/// Constructs a PluginLoadContext for the given plugin package directory.
		/// </summary>
		/// <param name="name">Name for the load context (typically the plugin package name).</param>
		/// <param name="pluginDirectory">Full path of the plugin package directory.</param>
		public PluginLoadContext(string name, string pluginDirectory) : base(name, isCollectible: true)
		{
			this.pluginDirectory = pluginDirectory;
		}
		/// <inheritdoc/>
		protected override Assembly Load(AssemblyName assemblyName)
		{
			// Never load a plugin-local copy of an assembly which the host already has loaded (WebProxy.Plugins, BPUtil, etc); types must unify with the host's copy.
			foreach (Assembly a in Default.Assemblies)
			{
				if (string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
					return null; // Defer to the default load context.
			}
			string candidate = Path.Combine(pluginDirectory, assemblyName.Name + ".dll");
			if (File.Exists(candidate))
				return LoadAssemblyFromFileBytes(candidate);
			return null; // Defer to the default load context.
		}
		/// <summary>
		/// Loads an assembly into this load context from the bytes of the given DLL file (so the file is not locked).  If a matching .pdb file exists, it is loaded too so that plugin stack traces have line numbers.
		/// </summary>
		/// <param name="dllPath">Full path of the DLL file to load.</param>
		/// <returns>The loaded assembly.</returns>
		public Assembly LoadAssemblyFromFileBytes(string dllPath)
		{
			byte[] assemblyBytes = File.ReadAllBytes(dllPath);
			string pdbPath = Path.ChangeExtension(dllPath, ".pdb");
			using (MemoryStream msAssembly = new MemoryStream(assemblyBytes))
			{
				if (File.Exists(pdbPath))
				{
					using (MemoryStream msPdb = new MemoryStream(File.ReadAllBytes(pdbPath)))
						return LoadFromStream(msAssembly, msPdb);
				}
				return LoadFromStream(msAssembly);
			}
		}
	}
}

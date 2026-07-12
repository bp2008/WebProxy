using BPUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using WebProxy.Plugins;

namespace WebProxy
{
	/// <summary>
	/// A configured plugin instance which has been constructed and is ready to receive requests (or which failed to construct/load, in which case <see cref="Error"/> is set).
	/// </summary>
	public class RuntimePluginInstance
	{
		/// <summary>
		/// User-defined unique Id of the configured plugin instance.
		/// </summary>
		public string Id;
		/// <summary>
		/// Full name of the plugin type which this instance uses.
		/// </summary>
		public string PluginTypeName;
		/// <summary>
		/// The plugin object, or null if the instance is faulted.
		/// </summary>
		public WebProxyPlugin Plugin;
		/// <summary>
		/// If not null, the instance is faulted and requests routed to it will receive an error response (fail-closed).
		/// </summary>
		public string Error;
	}
	/// <summary>
	/// <para>Static manager for WebProxy plugins.</para>
	/// <para>Plugin packages are subdirectories of the "Plugins" directory within WebProxy's data folder.  Each package directory contains one or more DLL files; assemblies are loaded from bytes in a collectible AssemblyLoadContext so that package files can be replaced or deleted while the service runs.</para>
	/// <para>Configured plugin instances (from <see cref="Settings.pluginInstances"/>) are constructed by <see cref="OnSettingsChanged"/>, which is called at startup and whenever settings are saved.</para>
	/// </summary>
	public static class PluginManager
	{
		/// <summary>
		/// Full path of the directory containing plugin packages.
		/// </summary>
		public static string PluginsDirectory => Path.Combine(Globals.WritableDirectoryBase, "Plugins");
		private static readonly object syncLock = new object();
		private static bool packagesLoaded = false;
		private static volatile List<LoadedPluginPackage> packages = new List<LoadedPluginPackage>();
		private static volatile Dictionary<string, LoadedPluginType> typesByFullName = new Dictionary<string, LoadedPluginType>();
		private static volatile Dictionary<string, RuntimePluginInstance> instancesById = new Dictionary<string, RuntimePluginInstance>();
		private static List<PluginLoadContext> activeLoadContexts = new List<PluginLoadContext>();
		private static readonly RuntimePluginInstance[] noPlugins = new RuntimePluginInstance[0];

		/// <summary>
		/// Gets the list of installed plugin packages (loading them first, if necessary).  The returned list must be treated as read-only.
		/// </summary>
		public static List<LoadedPluginPackage> GetInstalledPackages()
		{
			EnsurePackagesLoaded();
			return packages;
		}
		/// <summary>
		/// Gets the status (Id and error state) of all configured plugin instances.  The returned collection is a snapshot.
		/// </summary>
		public static Dictionary<string, string> GetInstanceErrors()
		{
			Dictionary<string, RuntimePluginInstance> snapshot = instancesById;
			Dictionary<string, string> errors = new Dictionary<string, string>();
			foreach (RuntimePluginInstance rpi in snapshot.Values)
			{
				if (rpi.Error != null)
					errors[rpi.Id] = rpi.Error;
			}
			return errors;
		}
		/// <summary>
		/// Returns true if the given plugin type (by full name) is currently installed and loadable.
		/// </summary>
		/// <param name="pluginTypeFullName">Full name of a plugin type.</param>
		public static bool IsPluginTypeInstalled(string pluginTypeFullName)
		{
			EnsurePackagesLoaded();
			return pluginTypeFullName != null && typesByFullName.TryGetValue(pluginTypeFullName, out LoadedPluginType lpt) && lpt.Type != null;
		}
		/// <summary>
		/// <para>Called at startup and whenever settings are saved.  Ensures plugin packages are loaded and (re)constructs all configured plugin instances from the given settings object.</para>
		/// <para>Old plugin instances are notified via <see cref="WebProxyPlugin.OnUnloadingAsync"/> and discarded.</para>
		/// </summary>
		/// <param name="s">Settings object to construct plugin instances from.</param>
		public static void OnSettingsChanged(Settings s)
		{
			lock (syncLock)
			{
				EnsurePackagesLoaded();
				RebuildInstances(s);
			}
		}
		/// <summary>
		/// Unloads and reloads all plugin packages from disk, then reconstructs all configured plugin instances.  Call after plugin files have been added, replaced, or deleted.
		/// </summary>
		public static void ReloadPackages()
		{
			lock (syncLock)
			{
				LoadPackagesInternal();
				RebuildInstances(WebProxyService.MakeLocalSettingsReference());
			}
		}
		/// <summary>
		/// <para>Installs (or upgrades) a plugin from an uploaded DLL file.  The DLL is written to a package directory named after the DLL file, then all plugin packages are reloaded.</para>
		/// <para>Throws an exception with a user-friendly message if the file name or content is unacceptable.</para>
		/// </summary>
		/// <param name="fileName">Original file name of the uploaded DLL, e.g. "MyPlugin.dll".</param>
		/// <param name="dllBytes">File content.</param>
		/// <returns>The name of the plugin package which was created or updated.</returns>
		public static string InstallPluginDll(string fileName, byte[] dllBytes)
		{
			if (dllBytes == null || dllBytes.Length == 0)
				throw new Exception("The uploaded file was empty.");
			fileName = (fileName ?? "").Trim();
			string safeFileName = StringUtil.MakeSafeForFileName(fileName);
			if (string.IsNullOrWhiteSpace(safeFileName) || !safeFileName.IEndsWith(".dll") || safeFileName.Length <= ".dll".Length)
				throw new Exception("The uploaded file must be a DLL file with a valid file name.");
			string packageName = safeFileName.Substring(0, safeFileName.Length - ".dll".Length);

			string packageDir = Path.Combine(PluginsDirectory, packageName);
			Directory.CreateDirectory(packageDir);
			File.WriteAllBytes(Path.Combine(packageDir, safeFileName), dllBytes);

			ReloadPackages();
			return packageName;
		}
		/// <summary>
		/// Deletes the given plugin package's directory and reloads all plugin packages.  Configured plugin instances which used the deleted plugin remain in the settings, but become faulted ("not installed") until the plugin is reinstalled or the instances are deleted.
		/// </summary>
		/// <param name="packageName">Name of the plugin package to delete.</param>
		public static void DeletePluginPackage(string packageName)
		{
			packageName = (packageName ?? "").Trim();
			if (string.IsNullOrWhiteSpace(packageName) || packageName != StringUtil.MakeSafeForFileName(packageName))
				throw new Exception("Invalid plugin package name.");
			string packageDir = Path.Combine(PluginsDirectory, packageName);
			if (!Directory.Exists(packageDir))
				throw new Exception("Plugin package \"" + packageName + "\" was not found.");
			Robust.Retry(() =>
			{
				Directory.Delete(packageDir, true);
			}, 50, 100, 200, 400, 800);
			ReloadPackages();
		}
		/// <summary>
		/// <para>Gets the configured plugin instances which apply to a request that matched the given Entrypoint and Exitpoint, in order (Entrypoint attachments first, then Exitpoint attachments, skipping duplicates).</para>
		/// <para>Plugin instance Ids which are attached but not configured are returned as faulted instances so that the caller can fail closed.</para>
		/// </summary>
		/// <param name="entrypoint">The Entrypoint which matched the request.</param>
		/// <param name="exitpoint">The Exitpoint which matched the request.</param>
		/// <returns>Array of applicable plugin instances.  May be empty, never null.</returns>
		public static RuntimePluginInstance[] GetPluginsForRequest(Entrypoint entrypoint, Exitpoint exitpoint)
		{
			string[] entrypointPlugins = entrypoint?.plugins;
			string[] exitpointPlugins = exitpoint?.plugins;
			int totalAttached = (entrypointPlugins?.Length ?? 0) + (exitpointPlugins?.Length ?? 0);
			if (totalAttached == 0)
				return noPlugins;

			Dictionary<string, RuntimePluginInstance> snapshot = instancesById;
			List<RuntimePluginInstance> result = new List<RuntimePluginInstance>(totalAttached);
			HashSet<string> addedIds = new HashSet<string>();
			foreach (string[] idArray in new string[][] { entrypointPlugins, exitpointPlugins })
			{
				if (idArray == null)
					continue;
				foreach (string id in idArray)
				{
					if (string.IsNullOrWhiteSpace(id) || !addedIds.Add(id))
						continue;
					if (snapshot.TryGetValue(id, out RuntimePluginInstance rpi))
						result.Add(rpi);
					else
						result.Add(new RuntimePluginInstance() { Id = id, Error = "The plugin instance \"" + id + "\" is attached but is not configured." });
				}
			}
			return result.ToArray();
		}

		private static void EnsurePackagesLoaded()
		{
			if (packagesLoaded)
				return;
			lock (syncLock)
			{
				if (!packagesLoaded)
				{
					LoadPackagesInternal();
					packagesLoaded = true;
				}
			}
		}
		/// <summary>
		/// Loads all plugin packages from disk, swapping out any previously loaded packages.  Must be called within a lock on <see cref="syncLock"/>.
		/// </summary>
		private static void LoadPackagesInternal()
		{
			// Ensure shared contract assemblies are loaded into the default load context before any plugin assembly loads, so that plugin-local copies of these assemblies are never used.
			_ = typeof(WebProxyPlugin).Assembly;
			_ = typeof(BPUtil.Globals).Assembly;

			List<LoadedPluginPackage> newPackages = new List<LoadedPluginPackage>();
			Dictionary<string, LoadedPluginType> newTypes = new Dictionary<string, LoadedPluginType>();
			List<PluginLoadContext> newContexts = new List<PluginLoadContext>();

			DirectoryInfo pluginsDir = new DirectoryInfo(PluginsDirectory);
			if (pluginsDir.Exists)
			{
				foreach (DirectoryInfo packageDir in pluginsDir.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
				{
					LoadedPluginPackage package = LoadOnePackage(packageDir, newTypes, newContexts);
					newPackages.Add(package);
				}
			}

			List<PluginLoadContext> oldContexts = activeLoadContexts;
			packages = newPackages;
			typesByFullName = newTypes;
			activeLoadContexts = newContexts;

			// Best-effort unload of the old load contexts.  Even if unloading is delayed by lingering references, old packages are no longer reachable via the plugin registry, and plugin files on disk are not locked (assemblies were loaded from bytes).
			foreach (PluginLoadContext oldContext in oldContexts)
			{
				try
				{
					oldContext.Unload();
				}
				catch (Exception ex)
				{
					Logger.Debug(ex, "PluginManager: Failed to unload plugin load context \"" + oldContext.Name + "\".");
				}
			}
		}
		private static LoadedPluginPackage LoadOnePackage(DirectoryInfo packageDir, Dictionary<string, LoadedPluginType> newTypes, List<PluginLoadContext> newContexts)
		{
			LoadedPluginPackage package = new LoadedPluginPackage();
			package.PackageName = packageDir.Name;
			List<string> errors = new List<string>();
			try
			{
				FileInfo[] dllFiles = packageDir.GetFiles("*.dll", SearchOption.TopDirectoryOnly);
				package.Files = dllFiles.Select(f => f.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
				if (dllFiles.Length == 0)
				{
					package.LoadError = "No DLL files were found in the package directory.";
					return package;
				}

				PluginLoadContext loadContext = new PluginLoadContext(packageDir.Name, packageDir.FullName);
				newContexts.Add(loadContext);

				foreach (FileInfo dllFile in dllFiles)
				{
					string simpleName = Path.GetFileNameWithoutExtension(dllFile.Name);
					// Skip copies of assemblies which the host already provides (e.g. BPUtil.dll or WebProxy.Plugins.dll accidentally deployed with the plugin).
					if (AssemblyLoadContext.Default.Assemblies.Any(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase)))
						continue;
					try
					{
						Assembly assembly = loadContext.LoadAssemblyFromFileBytes(dllFile.FullName);
						foreach (Type t in GetLoadableTypes(assembly))
						{
							if (t == null || t.IsAbstract || !t.IsPublic || !typeof(WebProxyPlugin).IsAssignableFrom(t))
								continue;
							LoadedPluginType lpt = DescribePluginType(packageDir.Name, assembly, t);
							if (newTypes.TryAdd(lpt.TypeFullName, lpt))
								package.PluginTypes.Add(lpt);
							else
								errors.Add("Plugin type \"" + lpt.TypeFullName + "\" is also provided by package \"" + newTypes[lpt.TypeFullName].PackageName + "\".  The copy in this package is ignored.");
						}
					}
					catch (Exception ex)
					{
						errors.Add(dllFile.Name + ": " + ex.FlattenMessages());
					}
				}
				if (package.PluginTypes.Count == 0 && errors.Count == 0)
					errors.Add("No plugin types (public non-abstract classes deriving from WebProxyPlugin) were found in this package.");
			}
			catch (Exception ex)
			{
				errors.Add(ex.FlattenMessages());
			}
			if (errors.Count > 0)
				package.LoadError = string.Join(Environment.NewLine, errors);
			return package;
		}
		private static LoadedPluginType DescribePluginType(string packageName, Assembly assembly, Type t)
		{
			LoadedPluginType lpt = new LoadedPluginType();
			lpt.PackageName = packageName;
			lpt.TypeFullName = t.FullName;
			lpt.Name = t.Name;
			lpt.Version = assembly.GetName().Version?.ToString();
			try
			{
				// Construct a prototype instance to read the plugin's Description and options schema.  Plugin constructors are required to be trivial.
				WebProxyPlugin prototype = (WebProxyPlugin)Activator.CreateInstance(t);
				lpt.Description = prototype.Description;
				lpt.OptionFields = PluginOptionSchema.BuildSchema(prototype.OptionsType);
				lpt.Type = t;
			}
			catch (Exception ex)
			{
				lpt.Description = "ERROR: This plugin type could not be constructed: " + ex.FlattenMessages();
				lpt.OptionFields = new List<PluginOptionField>();
			}
			return lpt;
		}
		private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				return ex.Types.Where(t => t != null);
			}
		}
		/// <summary>
		/// Constructs plugin instances from the given settings, replacing all previously constructed instances.  Must be called within a lock on <see cref="syncLock"/>.
		/// </summary>
		private static void RebuildInstances(Settings s)
		{
			Dictionary<string, LoadedPluginType> types = typesByFullName;
			Dictionary<string, RuntimePluginInstance> newInstances = new Dictionary<string, RuntimePluginInstance>();
			if (s?.pluginInstances != null)
			{
				foreach (PluginInstance config in s.pluginInstances)
				{
					if (config == null || string.IsNullOrWhiteSpace(config.Id) || newInstances.ContainsKey(config.Id))
						continue;
					RuntimePluginInstance rpi = new RuntimePluginInstance();
					rpi.Id = config.Id;
					rpi.PluginTypeName = config.PluginTypeName;
					try
					{
						if (!types.TryGetValue(config.PluginTypeName ?? "", out LoadedPluginType lpt) || lpt.Type == null)
							rpi.Error = "The plugin type \"" + config.PluginTypeName + "\" is not installed.";
						else
						{
							WebProxyPlugin plugin = (WebProxyPlugin)Activator.CreateInstance(lpt.Type);
							object options = null;
							if (plugin.OptionsType != null)
							{
								if (config.Options != null)
									options = config.Options.ToObject(plugin.OptionsType);
								else
									options = Activator.CreateInstance(plugin.OptionsType);
							}
							plugin.InitializePlugin(new PluginHostImpl(config.Id), config.Id, options);
							TaskHelper.RunAsyncCodeSafely(() => plugin.OnLoadedAsync());
							rpi.Plugin = plugin;
						}
					}
					catch (Exception ex)
					{
						rpi.Error = ex.FlattenMessages();
						WebProxyService.ReportError(ex, "Failed to load plugin instance \"" + config.Id + "\" (" + config.PluginTypeName + ").");
					}
					newInstances[rpi.Id] = rpi;
				}
			}

			Dictionary<string, RuntimePluginInstance> oldInstances = instancesById;
			instancesById = newInstances;

			foreach (RuntimePluginInstance old in oldInstances.Values)
			{
				if (old.Plugin != null)
				{
					try
					{
						TaskHelper.RunAsyncCodeSafely(() => old.Plugin.OnUnloadingAsync());
					}
					catch (Exception ex)
					{
						WebProxyService.ReportError(ex, "Plugin instance \"" + old.Id + "\" threw an exception while unloading.");
					}
				}
			}
		}
	}
}

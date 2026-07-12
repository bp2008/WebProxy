# WebProxy Plugins

WebProxy supports plugins: DLL files which extend WebProxy with custom behavior, similar to advanced Middlewares that are not baked into the WebProxy release.  Plugins can:

* Inspect and modify incoming requests (HTTP method, headers, and the destination URI a request will be proxied to).
* Answer requests themselves (fully-buffered or streaming responses), bypassing the normal proxying code path.
* Close a client's connection without a response (e.g. banning behavior).
* Hook into the proxying pipeline after response headers are received from the destination server, to inspect or modify the response status, headers, and body (streaming or fully-buffered).
* Expose custom options which are editable in the WebProxy Admin Console, individually for each configured plugin instance.

Plugin *instances* are configured on the **Plugins** page of the WebProxy Admin Console.  Plugin *files* are managed by an administrator with shell access to the server, or optionally via the Admin Console (disabled by default); see [Managing plugin files](#managing-plugin-files).

## Concepts

* **Plugin package**: A subdirectory of the `Plugins` directory inside WebProxy's data folder, containing one or more DLL files.  For example, the package `Plugins/MyPlugin/` containing `MyPlugin.dll`.  See [Managing plugin files](#managing-plugin-files).
* **Plugin type**: A public non-abstract class deriving from `WebProxyPlugin`, discovered inside a package's DLLs.  One package may provide multiple plugin types.
* **Plugin instance**: A plugin type combined with a set of option values, defined on the Plugins page and identified by a user-defined name.  Like a Middleware, a plugin instance takes effect when it is attached to Entrypoints or Exitpoints.  Because options belong to the instance, you can attach the same plugin to different websites with different options by creating multiple instances.

Requests only reach plugins after WebProxy's built-in middlewares (IP whitelisting, authentication, etc) have accepted the request.  Plugin instances run in attachment order: instances attached to the matched Entrypoint first, then instances attached to the matched Exitpoint.

Assemblies are loaded from bytes in collectible `AssemblyLoadContext`s, so plugin files on disk are never locked by the running service and can be replaced or deleted at any time.

## Managing plugin files

### Manual management (the default)

Plugin files are managed by creating, replacing, or deleting package subdirectories of the `Plugins` directory inside WebProxy's data folder.  (The data folder path is displayed when the WebProxy executable is run interactively.)

```
<data folder>/Plugins/MyPlugin/MyPlugin.dll
```

* To install or upgrade a plugin, create the package subdirectory and copy in the plugin's DLL file(s), plus any additional files the plugin needs (e.g. dependency DLLs or native libraries).
* To uninstall a plugin, delete its package subdirectory.  Configured plugin instances which used the deleted plugin remain in the settings, but become faulted ("not installed") until the plugin is reinstalled or the instances are deleted.

WebProxy does not watch the `Plugins` directory for changes; after manually adding, replacing, or deleting plugin files, **restart the WebProxy service** so it loads the current set of plugin files.

### Remote management via the Admin Console (disabled by default)

The **Plugins** page of the Admin Console can also upload and delete plugin packages, but this capability is **disabled by default** because it is dangerous: plugins are .NET code which runs with the privileges of the WebProxy process, so the ability to remotely install a plugin DLL is equivalent to remote code execution for anyone with authenticated Admin Console access.

Remote plugin file management is controlled by the `AllowRemotePluginFileManagement` flag (default: `false`) in the `SecureSettings.json` file in WebProxy's data folder.  Unlike `Settings.json`, the `SecureSettings.json` file can not be imported or modified through the Admin Console, so the flag can only be enabled by an administrator with shell access to the machine running WebProxy, using any of these methods:

* **Any platform**: Edit `SecureSettings.json` in WebProxy's data folder and set `"AllowRemotePluginFileManagement": true`.  The file is re-read on demand, so the change takes effect immediately; no service restart is needed.
* **Linux**: Run the WebProxy executable interactively and use the `enableremoteplugins` command.  The related commands `remoteplugins` (display the current state) and `disableremoteplugins` are also available.
* **Windows**: Run `WebProxy.exe` to open the Service Manager GUI and click the **Remote Plugin Mgmt** button.

The flag only controls the ability to remotely add or remove plugin files.  Authenticated Admin Console users can create, configure, attach, and delete *plugin instances* of already-installed plugins regardless of the state of this flag.

To return to the secure default, set the flag back to `false` using any of the methods above, or use the **Disable Remote Plugin File Management** button in the **Settings** section of the Admin Console (the web console can disable the flag, but never enable it).

## Creating a plugin

1. Create a **class library** project targeting a compatible .NET version, e.g. `net10.0`.  Do not use a platform-specific target framework (like `net10.0-windows`) and do not use platform-specific dependencies; this keeps the same DLL usable on Windows and Linux.
2. Reference `WebProxy.Plugins.dll` and `BPUtil.dll` from a WebProxy installation (or via project references if building within the WebProxy source tree):

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
	</PropertyGroup>
	<ItemGroup>
		<Reference Include="WebProxy.Plugins">
			<HintPath>path\to\WebProxy\WebProxy.Plugins.dll</HintPath>
			<Private>false</Private>
		</Reference>
		<Reference Include="BPUtil">
			<HintPath>path\to\WebProxy\BPUtil.dll</HintPath>
			<Private>false</Private>
		</Reference>
	</ItemGroup>
</Project>
```

`<Private>false</Private>` prevents these DLLs from being copied to your output folder.  **Do not deploy `BPUtil.dll` or `WebProxy.Plugins.dll` with your plugin** — WebProxy provides them at runtime (plugin-local copies of assemblies the host already provides are ignored).

3. Create one or more public classes deriving from `WebProxyPlugin` or `WebProxyPlugin<TOptions>`:

```csharp
using System.Threading.Tasks;
using WebProxy.Plugins;

public class MyPluginOptions
{
	[PluginOption(DisplayName = "Log File Path",
		HelpText = "Full path of the log file.  If empty, a default inside the plugin's data directory is used.")]
	public string LogFilePath = "";

	[PluginOption(DisplayName = "Verbosity")]
	public MyVerbosity Verbosity = MyVerbosity.Basic;
}
public enum MyVerbosity { Basic, Detailed }

public class MyPlugin : WebProxyPlugin<MyPluginOptions>
{
	public override string Description => "Shown in the Admin Console.";

	public override async Task<PluginRequestAction> OnRequestAsync(PluginRequestContext context)
	{
		// ... use context and Options here (see below) ...
		return PluginRequestAction.Continue;
	}
}
```

4. Build, then install the DLL (see [Managing plugin files](#managing-plugin-files)): copy it to `<data folder>/Plugins/<PluginName>/` and restart the WebProxy service, or — if remote plugin file management has been enabled — upload it on the **Plugins** page of the Admin Console.
5. Create a **Plugin Instance** on the Plugins page, configure its options, and attach it to Entrypoints and/or Exitpoints (checkbox lists in the Entrypoint/Exitpoint editors, below Middlewares).

An example plugin project with four plugins (`CustomRequestLogger`, `UserAgentBlocker`, `TextReplaceInResponse`, `DestinationRewriter`) is included in the WebProxy source tree at [WebProxy.ExamplePlugin](WebProxy.ExamplePlugin).

## The request hook

`OnRequestAsync(PluginRequestContext context)` is called for every request that matched an Entrypoint or Exitpoint the plugin instance is attached to.  The context provides:

* `Processor` — the BPUtil `HttpProcessor` handling the connection (client IP, TLS state, and full request/response access).
* `Request` — the client's request.  You may modify `Request.HttpMethod` and `Request.Headers` before the request is proxied.  Hop-by-hop headers (`Connection`, `Host`, ...) are computed by the proxy and cannot be overridden, but `Accept-Encoding` *can* be (set it to `identity` if you intend to manipulate the response body and don't want to deal with compression).
* `DestinationUri` — where the request will be proxied to (scheme, host, port, path, query).  Assign a new absolute URI to reroute the request.  Only meaningful when `RequestWillBeProxied` is true (Exitpoint type `WebProxy`).
* `DestinationHostHeader` — optional override for the outgoing `Host` header / TLS SNI.
* `EntrypointName`, `ExitpointName`, `CancellationToken`.

Return value:

* `PluginRequestAction.Continue` — proceed normally (remaining plugins run, then the request is proxied or otherwise handled).
* `PluginRequestAction.Handled` — the plugin has answered the request itself.  Either write/configure a response first (e.g. `context.Response.FullResponseUTF8(...)`, `context.Response.Simple("403 Forbidden")`), or stream a response by configuring `context.Response.Set(...)` and writing to the stream from `context.Response.GetResponseStreamAsync()`.
* `PluginRequestAction.CloseConnection` — close the client's connection without any response.

If `OnRequestAsync` throws, WebProxy responds `500 Internal Server Error` and does not proxy the request (fail-closed).  Similarly, if a plugin instance is attached but faulted (failed to load, or its plugin was uninstalled), matching requests receive a 500 response rather than silently skipping the plugin.

## Response hooks

During `OnRequestAsync`, a plugin may register a response hook:

```csharp
context.AddResponseHeadersHook(async (PluginResponseContext rc) =>
{
	// Runs after response headers are received from the destination server,
	// before anything is sent to the client.
	rc.Response.Headers["X-Powered-By"] = "MyPlugin";     // modify headers
	string status = rc.Response.StatusString;              // inspect/modify status
});
```

Response hooks only run for proxied requests (`RequestWillBeProxied == true`).  Within a response hook, the plugin can additionally hook the response body:

**Buffered transform** (simplest; handles `Content-Encoding` decode and `Content-Length` fixup automatically):

```csharp
rc.SetBufferedBodyTransform(async (byte[] body) =>
{
	byte[] newBody = Transform(body);           // e.g. webp -> jpeg, minify JS...
	rc.Response.Headers["Content-Type"] = "image/jpeg";
	return newBody;
});
```

**Streaming filter** (full control; runs before response headers are written, so headers may still be modified inside the filter):

```csharp
rc.SetResponseBodyFilter(async (Stream source) =>
{
	// Return the original stream to leave the body unmodified, or return a
	// replacement stream (e.g. a wrapping stream that transforms data as it
	// is read).  The source stream has transfer encoding already decoded but
	// may still be compressed per the Content-Encoding response header.
	return new MyTransformingStream(source);
});
```

If a body filter replaces the stream, WebProxy automatically clears the `Content-Length` header before running filters (chunked transfer encoding or connection-close framing is used as needed); a filter may set `Response.ContentLength` itself if it knows the final length.  If the filter chain returns the original stream unchanged, the original `Content-Length` is restored.

If a response hook or body filter throws, the proxied request is aborted.

## Options

Declare options by deriving from `WebProxyPlugin<TOptions>`.  The options class needs a public parameterless constructor which assigns default values.  Public fields and public read/write properties of these types are editable in the Admin Console:

| .NET type | Editor |
|---|---|
| `string` | text input (or text area with `[PluginOption(Multiline = true)]`) |
| `bool` | checkbox |
| `int`, `long`, `double`, other numeric types | number input (`Min`/`Max` supported via `[PluginOption]`) |
| any `enum` | dropdown |
| `string[]`, `List<string>` | add/remove list of text inputs |

Use `[PluginOption]` to set `DisplayName`, `HelpText` (shown when help is toggled on), and `Placeholder`.  Unsupported member types are not shown in the Admin Console (but still round-trip through the settings file).

Option values are stored in `Settings.json` with the plugin instance and deserialized into your options class when instances are (re)built.  Access them via the strongly-typed `Options` property.

## Lifecycle and host services

* The plugin class is instantiated once per configured plugin instance.  **Constructors must be trivial and must not throw** (a throwaway instance is also constructed at package load time to read `Description` and the options schema).
* `OnLoadedAsync()` — called after `Host`, `InstanceId`, and `OptionsObject` are assigned.  Do expensive initialization here.  Throwing marks the instance faulted.
* `OnRequestAsync(...)` — may be called concurrently by many requests; synchronize any shared mutable state.
* `OnUnloadingAsync()` — called when the instance is discarded.  Instances are discarded and recreated whenever WebProxy settings are saved or plugin files change.

`Host` (an `IPluginHost`) provides:

* `Log(message)` / `LogError(ex, info)` — write to WebProxy's log (and error tracker), prefixed with the instance Id.
* `DataDirectoryPath` — a writable directory reserved for this plugin instance (`<data>/PluginData/<InstanceId>/`), created on first use.
* `ServerDataDirectoryPath` — WebProxy's data directory.
* `HostVersion` — the WebProxy version.

## Cross-platform notes

The same plugin DLL runs on Windows and Linux as long as it targets a platform-neutral framework (e.g. `net10.0`) and avoids platform-specific APIs and native dependencies.  If a plugin needs native dependencies, place the appropriate runtime files in the plugin's package directory manually.

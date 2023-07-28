# WebProxy
An HTTP(S) reverse proxy server with web-based configuration.

## Features
* Web-based configuration GUI
* Automatic certificates from LetsEncrypt (optional)
  * Usage of this feature indicates acceptance of [LetsEncrypt's Terms of Service](https://community.letsencrypt.org/tos)
  * Supported validation methods include `HTTP-01` and `TLS-ALPN-01`.  WebProxy will automatically select between validation methods `HTTP-01` or `TLS-ALPN-01` with a preference for `HTTP-01` if both ports are available.
  * `HTTP-01` validation requires the domain to be reachable via the internet using HTTP on port 80.
  * `TLS-ALPN-01` validation requires the domain to be reachable via the internet using HTTPS on port 443.
* WebSocket support
* TLS 1.2 support
* TLS 1.3 support on operating systems that provide it via SslStream interface (Windows 11, Server 2022, maybe others).
* Middleware system to manipulate proxy requests or add usage constraints.  Middlewares can be enabled individually for each proxied website.  Available middleware types:
  * `IPWhitelist` - Provides IP whitelisting of client connections.
  * `HttpDigestAuth` - This middleware causes requests to require HTTP Digest Authentication.
  * `RedirectHttpToHttps` - Requests coming in using plain HTTP are redirected to HTTPS. Requires the Entrypoint to support both HTTP and HTTPS.
  * `AddHttpHeaderToResponse` - Adds a user-defined static HTTP header to proxied responses.
  * `AddProxyServerTiming` - Adds a `Server-Timing` HTTP header to proxied responses which includes timing details for the proxied connection.  This information appears in a web browser's developer console when you inspect a network request's timing.
  * `XForwardedFor` - Adds an `X-Forwarded-For` header according to rules you specify.
  * `XForwardedHost` - Adds an `X-Forwarded-Host` header according to rules you specify.
  * `XForwardedProto` - Adds an `X-Forwarded-Proto` header according to rules you specify.
  * `XRealIp` - Adds an `X-Real-Ip` header according to rules you specify.
  * `TrustedProxyIPRanges` - Allows you to specify which client IP addresses are trusted to provide proxy-related headers such as `X-Forwarded-For`.


### Windows Installation

Download the Windows release and run WebProxy.exe.  Click "Install", then "Start".  Click the "Admin Console" button to open a panel containing the admin credentials and links to the Admin Console website.

![image](https://github.com/bp2008/WebProxy/assets/5639911/bba6804d-4d80-4349-b560-b2171245a53d)

### Linux Installation Script

Run these commands on your linux machine:

```
wget https://raw.githubusercontent.com/bp2008/WebProxy/master/WebProxy/webproxy_install.sh
chmod u+x webproxy_install.sh
./webproxy_install.sh
```

The installation script will ask if you wish to install or uninstall.  Once installed, WebProxy will start automatically.

Access the interactive command line interface by running WebProxy with the argument "cmd":

```
sudo /usr/bin/dotnet ~/webproxy/WebProxyLinux.dll cmd
```

Within the interactive command line interface, use the command `admin` to see the URLs and credentials for the Admin Console website.


If you prefer, you can manage the webproxy service via standard `systemctl` commands (the service name is `webproxy`).

### Building From Source

Requirements:
* [Visual Studio 2022 or newer](https://visualstudio.microsoft.com/downloads/)
* [.NET 6.0 SDK](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks)
* [BPUtil utility library source](https://github.com/bp2008/BPUtil)
* [Node.js (tested with version 18)](https://nodejs.org/en/download) to build the Admin Console website

WebProxy and BPUtil repositories must be downloaded/cloned separately.  To avoid needing to repair Project references, it is recommended to place both repositories in the same parent directory, e.g.
* `~/Repos/bp2008/BPUtil/BPUtil.sln`
* `~/Repos/bp2008/WebProxy/WebProxy.sln`

To build WebProxy:
1. Go to `~/Repos/bp2008/WebProxy/WebProxy/WebProxy-Admin` in a command line terminal and run `npm install`.  This will download all JavaScript dependencies.  This only needs to be done once initially, and then again after you modify dependencies in package.json.
2. In Visual Studio, Build > Build Solution.

Notes:
* Output will be in the `bin/Debug` or `bin/Release` folders.
  * The cross-platform build (WebProxyLinux) will be located in a subfolder called `net6.0`.
  * The Windows build (WebProxy) will be located in a subfolder called `net6.0-windows`.
* In order for WebProxyLinux's post-build event to complete successfully, it must be built last, after WebProxy has finished.  This serial ordering was set up by right clicking the WebProxyLinux project in Solution Explorer and choosing Build Dependencies > Project Dependencies.  In this dialog, WebProxy was selected as a dependency of WebProxyLinux.


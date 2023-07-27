# WebProxy
An HTTP(S) reverse proxy server with web-based configuration.

## Features
* Web-based configuration GUI
* (optional) Automatic certificates from LetsEncrypt
* WebSocket support
* TLS 1.2 support
* TLS 1.3 support on operating systems that provide it via SslStream interface (Windows 11, Server 2022, maybe others).

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

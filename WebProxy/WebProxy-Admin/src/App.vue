<template>
	<div class="sidebarUnsavedChanges left" v-if="configurationChanged"></div>
	<div class="sidebarUnsavedChanges right" v-if="configurationChanged"></div>
	<div :class="{ topBar: true, configurationChanged: configurationChanged }">
		<h1>
			WebProxy
			<template v-if="store.windowWidth > 600">
				Admin Console
			</template>
			{{appVersion}}
		</h1>
		<button v-if="configurationChanged" class="saveChanges" @click="saveChanges">Save Changes</button>
	</div>
	<div class="hiddenFormToPreventAutofill">
		<input type="text" name="username" value="" />
		<input type="password" name="password" value="" />
	</div>
	<div v-if="loading">Loading...</div>
	<div class="adminBody" v-else>
		<loading v-model:active="showFullscreenLoader"
				 :can-cancel="false"
				 :is-full-page="true" />
		<div class="tabBar" id="tabBar">
			<div v-for="tab in tabs" :class="{ tab: true, selectedTab: selectedTab === tab }" role="button" @click="selectTab(tab)">
				{{tab.Name}}
			</div>
		</div>
		<div class="adminContent">
			<div class="invisibleLine"></div>
			<div :class="{ helpToggle: true, enabled: store.showHelp }" @click="store.showHelp = !store.showHelp" title="toggle help text">?</div>
			<div v-if="selectedTab.Name === 'All' || selectedTab.Name === 'Settings'">
				<h2>Global Settings</h2>
				<div class="primaryContainer">
					<div class="flexRow">
						<label>LetsEncrypt Account Email</label>
						<input type="text" v-model="store.acmeAccountEmail" autocomplete="off" />
						<div class="comment" v-if="store.showHelp">Required for automatic certificate management.  Account will be created upon first use.  You may change the email address after creating the account.</div>
					</div>
					<div class="flexRow">
						<label>ErrorTracker Submission URL</label>
						<PasswordInput v-model="store.errorTrackerSubmitUrl" class="password-container" />
						<div class="comment" v-if="store.showHelp">Optional submit URL for an <a href="https://github.com/bp2008/ErrorTracker" target="_blank">ErrorTracker</a> instance.</div>
					</div>
					<div class="flexRow">
						<label>Cloudflare API Token</label>
						<PasswordInput v-model="store.cloudflareApiToken" class="password-container" />
						<div class="comment" v-if="store.showHelp">
							Optional.  If set, automatic certificate validation via Cloudflare DNS will be an option for Exitpoints.  DNS validation means your WebProxy server does not need to be accessible on any public port (but outgoing internet access is still required).  DNS validation will allow you to enter a wildcard domain in the Exitpoint's [Host Binding] and still use LetsEncrypt.<br /><br />
							This must be a <a href="https://dash.cloudflare.com/profile/api-tokens" target="_blank">Cloudflare API Token</a>, not a Global API Key, and the token needs permissions <span class="icode">Zone &gt; Zone &gt; Read</span> and <span class="icode">Zone &gt; DNS &gt; Edit</span>.  Under Zone Resources, <span class="icode">Include &gt All zones</span>.
						</div>
						<div><input type="button" v-if="store.cloudflareApiToken" value="Test Cloudflare DNS" @click="testCloudflareDNS" title="Tests the Cloudflare API Token by attempting to add and remove a DNS TXT record from the account." /></div>
					</div>
					<div class="flexRow">
						<label><input type="checkbox" v-model="store.verboseWebServerLogs" /> Enable Verbose Web Server Logging</label>
						<div class="comment" v-if="store.showHelp">
							For troubleshooting purposes, the web server can be configured to use verbose logging.  All requests will be logged along with other details.
						</div>
					</div>
					<div class="flexRow">
						<label>Max Concurrent Connection Count</label>
						<input type="number" v-model="store.serverMaxConnectionCount" min="8" max="10000" autocomplete="off" />
						<div class="comment" v-if="store.showHelp">
							<div>
								<span class="icode">N: [8-10000; Default: 1024]</span>
							</div>
							<div>
								The server will allow <span class="icode">N</span> connections to be processed concurrently.  While more than <span class="icode">N/2</span> connections are being processed, the server is in high-load mode which prevents the use of <span class="icode">Connection: keep-alive</span>.  When the limit is reached, additional connections are rejected with an HTTP 503 response saying "Server too busy".
							</div>
						</div>
					</div>
					<div class="flexRow">
						<label>Garbage Collector Mode: {{store.gcModeServer ? "Server" : "Workstation"}}</label>
						<div><input type="button" value="Enable Server GC" @click="EnableServerGC" /> <input type="button" value="Enable Workstation GC" @click="DisableServerGC" /></div>
						<div class="comment" v-if="store.showHelp">
							<p>The "Workstation" garbage collector runs more often than the "Server" GC, and generally results in a smaller memory footprint.</p>
							<p>The "Server" garbage collector is tuned for higher throughput and scalability.</p>
							<p>It is said that workstation garbage collection is always used on a computer that has only one logical CPU, regardless of the configuration setting.</p>
						</div>
					</div>
					<div class="flexRow" v-if="platformSupportsMemoryMax">
						<label>Process Memory Limit: {{memoryMaxCurrentValue}}</label>
						<div>
							<input type="number" v-model="memoryMax" min="100" max="100000" autocomplete="off" /> MiB <input type="button" value="<- Set New Limit" @click="SetMemoryMax(memoryMax)" /> <input type="button" value="Remove Limit" @click="DeleteMemoryMax()" />
						</div>
						<div class="comment" v-if="store.showHelp">
							The service will be restarted by systemd if the Working Set exceeds this amount.
						</div>
					</div>
					<div class="flexRow">
						<label>Admin Console Theme</label>
						<select v-model="store.currentTheme">
							<option v-for="t in store.themeList">{{t}}</option>
						</select>
						<div class="comment" v-if="store.showHelp">Only affects this web browser.</div>
					</div>
				</div>
			</div>
			<div v-show="selectedTab.Name === 'All' || selectedTab.Name === 'Entrypoints'">
				<h2>Entrypoints</h2>
				<p v-if="store.showHelp">Entrypoints define how the web server listens for incoming network requests.</p>
				<draggable v-model="store.entrypoints" handle=".dragHandle">
					<EntrypointEditor v-for="entrypoint in store.entrypoints" :key="entrypoint.uniqueId" :entrypoint="entrypoint" @delete="deleteEntrypoint(entrypoint)" />
				</draggable>
				<div class="buttonBar">
					<button @click="addEntrypoint()">Add New Entrypoint</button>
				</div>
			</div>
			<div v-show="selectedTab.Name === 'All' || selectedTab.Name === 'Exitpoints'">
				<h2>Exitpoints</h2>
				<p v-if="store.showHelp">An Exitpoint is a web destination which a client wants to reach.  This Admin Console is an Exitpoint, but an Exitpoint could also be another web server.</p>
				<draggable v-model="store.exitpoints" handle=".dragHandle">
					<ExitpointEditor v-for="exitpoint in store.exitpoints" :key="exitpoint.uniqueId" :exitpoint="exitpoint" @delete="deleteExitpoint(exitpoint)" @renew="renewCertificate(exitpoint)" @uploadCert="uploadCertificate" />
				</draggable>
				<div class="buttonBar">
					<button @click="addExitpoint()">Add New Exitpoint</button>
				</div>
			</div>
			<div v-show="selectedTab.Name === 'All' || selectedTab.Name === 'Middlewares'">
				<h2>Middlewares</h2>
				<p v-if="store.showHelp">A Middleware is a module which applies additional logic to Entrypoints or Exitpoints.  A Middleware is typically used for access control or to manipulate default WebProxy behavior in some way, such as by adding an HTTP header to all responses.</p>
				<draggable v-model="store.middlewares" handle=".dragHandle">
					<MiddlewareEditor v-for="middleware in store.middlewares" :key="middleware.uniqueId" :middleware="middleware" @delete="deleteMiddleware(middleware)" />
				</draggable>
				<div class="buttonBar">
					<button @click="addMiddleware()">Add New Middleware</button>
				</div>
			</div>
			<div v-show="selectedTab.Name === 'All' || selectedTab.Name === 'Routes'">
				<h2>Proxy Routes</h2>
				<p v-if="store.showHelp">The list of Proxy Routes defines which Exitpoints are reachable from which Entrypoints.  In order for an Exitpoint to be reachable by clients, it must be bound to at least one Entrypoint via a Proxy Route.</p>
				<draggable v-model="store.proxyRoutes" handle=".dragHandle">
					<ProxyRouteEditor v-for="proxyRoute in store.proxyRoutes" :key="proxyRoute.uniqueId" :proxyRoute="proxyRoute" @delete="deleteProxyRoute(proxyRoute)" />
				</draggable>
				<div class="buttonBar">
					<button @click="addProxyRoute()">Add New Proxy Route</button>
				</div>
			</div>
			<div v-show="selectedTab.Name === 'All' || selectedTab.Name === 'Dashboard'">
				<h2>Hosted URL Summary</h2>
				<HostedUrlSummary />
				<h2>Server Status</h2>
				<div><input type="button" value="Force Garbage Collection" @click="forceGarbageCollection" title="Force the server to perform full blocking garbage collection of all generations and compact the small and large object heaps." /></div>
				<div><br /></div>
				<ServerStatus />
			</div>
			<div v-if="selectedTab.Name === 'All' || selectedTab.Name === 'Settings'">
				<h2>Raw Settings.json</h2>
				View <a href="/Configuration/GetRaw" target="_blank">Settings.json</a>
				<h2>Log Files</h2>
				<div v-for="logFile in store.logFiles">
					<a :href="'/Log/' + logFile.fileName" target="_blank">{{logFile.fileName}}</a> ({{logFile.size}})
				</div>
				<h2>Export Settings and Certificates</h2>
				<a href="/Configuration/Export" target="_blank">WebProxy_Export.zip</a>
				<h2>Import Settings and Certificates</h2>
				<UploadFileControl label="Upload Zip" acceptFileExtension=".zip" @upload="uploadSettingsAndCertificatesClicked" />
			</div>
			<div v-show="selectedTab.Name === 'All' || selectedTab.Name === 'Log'">
				<h2>Live Log</h2>
				<LogReader />
			</div>
		</div>
	</div>
</template>

<script>
	import EntrypointEditor from './components/EntrypointEditor.vue';
	import ExitpointEditor from './components/ExitpointEditor.vue';
	import MiddlewareEditor from './components/MiddlewareEditor.vue';
	import ProxyRouteEditor from './components/ProxyRouteEditor.vue';
	import HostedUrlSummary from './components/HostedUrlSummary.vue';
	import ServerStatus from './components/ServerStatus.vue';
	import PasswordInput from './components/PasswordInput.vue';
	import LogReader from './components/LogReader.vue';
	import UploadFileControl from '/src/components/UploadFileControl.vue';
	import ExecAPI from './library/API';
	import store from '/src/library/store';
	import Loading from 'vue-loading-overlay';
	import { VueDraggableNext } from 'vue-draggable-next';

	export default {
		components: { EntrypointEditor, ExitpointEditor, MiddlewareEditor, ProxyRouteEditor, HostedUrlSummary, PasswordInput, LogReader, Loading, draggable: VueDraggableNext, UploadFileControl, ServerStatus },
		data()
		{
			return {
				store: store,
				loading: false,
				showFullscreenLoader: false,
				originalJson: null,
				selectedTab: {},
				tabs: [
					{ Name: "All", scrollTop: false },
					{ Name: "Dashboard", scrollTop: true },
					{ Name: "Settings", scrollTop: true },
					{ Name: "Entrypoints", scrollTop: true },
					{ Name: "Exitpoints", scrollTop: true },
					{ Name: "Middlewares", scrollTop: true },
					{ Name: "Routes", scrollTop: true },
					{ Name: "Log", scrollTop: true }
				],
				fileInputChangeCounter: 0,
				platformSupportsMemoryMax: false,
				memoryMax: null,
				memoryMaxCurrentValue: null
			};
		},
		created()
		{
			window.appRoot = this;
			try
			{
				let selectedTabName = localStorage.webProxySelectedTabName;
				for (let i = 0; i < this.tabs.length; i++)
				{
					let tab = this.tabs[i];
					if (tab.Name === selectedTabName)
					{
						this.selectedTab = tab;
						break;
					}
				}
			}
			catch { }
			if (!this.selectedTab.Name)
				this.selectedTab = this.tabs[1];
			this.getConfiguration();
		},
		computed:
		{
			currentJson()
			{
				return JSON.stringify({
					acmeAccountEmail: store.acmeAccountEmail,
					errorTrackerSubmitUrl: store.errorTrackerSubmitUrl,
					cloudflareApiToken: store.cloudflareApiToken,
					verboseWebServerLogs: store.verboseWebServerLogs,
					serverMaxConnectionCount: store.serverMaxConnectionCount,
					entrypoints: store.entrypoints,
					exitpoints: store.exitpoints,
					middlewares: store.middlewares,
					proxyRoutes: store.proxyRoutes
				});
			},
			configurationChanged()
			{
				return this.originalJson && this.currentJson !== this.originalJson;
			},
			appVersion()
			{
				return store ? store.appVersion : "";
			},
			selectedFile()
			{
				if (this.fileInputChangeCounter > 0 && this.$refs.fileInput.files.length > 0)
					return this.$refs.fileInput.files[0];
				return null;
			}
		},
		methods:
		{
			async getConfiguration()
			{
				try
				{
					this.loading = true;
					const response = await ExecAPI("Configuration/Get");
					this.loading = false;

					this.consumeConfigurationResponse(response);

					//const response = await toaster.promise(
					//	ExecAPI("Configuration/Get"),
					//	{
					//		pending: 'Loading...',
					//		success: 'Loaded',
					//		error: 'Loading failed!'
					//	}
					//);
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
				}
			},
			async saveChanges()
			{
				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("Configuration/Set", {
						acmeAccountEmail: store.acmeAccountEmail,
						errorTrackerSubmitUrl: store.errorTrackerSubmitUrl,
						cloudflareApiToken: store.cloudflareApiToken,
						verboseWebServerLogs: store.verboseWebServerLogs,
						serverMaxConnectionCount: store.serverMaxConnectionCount,
						entrypoints: store.entrypoints,
						exitpoints: store.exitpoints,
						middlewares: store.middlewares,
						proxyRoutes: store.proxyRoutes
					});

					this.consumeConfigurationResponse(response);

					if (response.adminEntryOrigins && response.adminEntryOrigins.length)
					{
						let alreadyOk = false;
						for (let i = 0; i < response.adminEntryOrigins.length; i++)
						{
							if (location.origin === response.adminEntryOrigins[i])
								alreadyOk = true;
						}
						if (!alreadyOk)
						{
							let bestOrigin = null;
							for (let i = 0; i < response.adminEntryOrigins.length; i++)
							{
								if (response.adminEntryOrigins[i].indexOf("https:") === 0)
									bestOrigin = response.adminEntryOrigins[i];
							}
							if (!bestOrigin)
								bestOrigin = response.adminEntryOrigins[0];
							if (bestOrigin)
							{
								location.href = bestOrigin;
								return;
							}
						}
					}
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
				}
				finally
				{
					this.showFullscreenLoader = false;
				}
			},
			consumeConfigurationResponse(response)
			{
				if (!response.success)
				{
					toaster.error(response.error, 60000);
					return;
				}
				// If adding fields to store, please add them in store.js too!
				store.exitpointTypes = response.exitpointTypes;
				store.middlewareTypes = response.middlewareTypes;
				store.proxyHeaderBehaviorOptions = response.proxyHeaderBehaviorOptions;
				store.proxyHeaderBehaviorOptionsDescriptions = response.proxyHeaderBehaviorOptionsDescriptions;
				store.tlsCipherSuiteSets = response.tlsCipherSuiteSets;
				store.tlsCipherSuiteSetDescriptions = response.tlsCipherSuiteSetDescriptions;
				store.tlsCipherSuitesPolicySupported = response.tlsCipherSuitesPolicySupported;
				store.acmeAccountEmail = response.acmeAccountEmail;
				store.errorTrackerSubmitUrl = response.errorTrackerSubmitUrl;
				store.cloudflareApiToken = response.cloudflareApiToken;
				store.verboseWebServerLogs = response.verboseWebServerLogs;
				store.serverMaxConnectionCount = response.serverMaxConnectionCount;
				store.logFiles = response.logFiles;
				store.gcModeServer = response.gcModeServer;
				store.appVersion = response.appVersion;
				if (response.platformSupportsMemoryMax)
				{
					this.platformSupportsMemoryMax = true;
					this.memoryMaxCurrentValue = response.memoryMax;
					if (response.memoryMax && response.memoryMax.indexOf("M") > -1)
						this.memoryMax = parseInt(response.memoryMax);
					else
						this.memoryMax = 1500;
					if (!this.memoryMaxCurrentValue)
						this.memoryMaxCurrentValue = "None";
				}

				for (let i = 0; i < response.entrypoints.length; i++)
					FixEntrypoint(response.entrypoints[i]);

				for (let i = 0; i < response.exitpoints.length; i++)
					FixExitpoint(response.exitpoints[i]);

				for (let i = 0; i < response.middlewares.length; i++)
					FixMiddleware(response.middlewares[i]);

				for (let i = 0; i < response.proxyRoutes.length; i++)
					FixProxyRoute(response.proxyRoutes[i]);

				store.entrypoints = response.entrypoints;
				store.exitpoints = response.exitpoints;
				store.middlewares = response.middlewares;
				store.proxyRoutes = response.proxyRoutes;

				this.originalJson = this.currentJson;
			},
			addEntrypoint()
			{
				store.entrypoints.push(FixEntrypoint({}));
			},
			addExitpoint()
			{
				store.exitpoints.push(FixExitpoint({}));
			},
			addMiddleware()
			{
				store.middlewares.push(FixMiddleware({}));
			},
			addProxyRoute()
			{
				store.proxyRoutes.push(FixProxyRoute({}));
			},
			deleteEntrypoint(entrypoint)
			{
				if (confirm("You are about to delete Entrypoint: " + entrypoint.name))
					DeleteFromArray(entrypoint, store.entrypoints);
			},
			deleteExitpoint(exitpoint)
			{
				if (confirm("You are about to delete Exitpoint: " + exitpoint.name))
					DeleteFromArray(exitpoint, store.exitpoints);
			},
			deleteMiddleware(middleware)
			{
				if (confirm("You are about to delete Middleware: " + middleware.Id))
					DeleteFromArray(middleware, store.middlewares);
			},
			deleteProxyRoute(proxyRoute)
			{
				if (confirm("You are about to delete ProxyRoute:\n\n" + proxyRoute.entrypointName + "\n -> \n" + proxyRoute.exitpointName))
					DeleteFromArray(proxyRoute, store.proxyRoutes);
			},
			async renewCertificate(exitpoint)
			{
				if (this.configurationChanged)
				{
					toaster.info("Please save the changes on this page, then try again to force renewal of the certificate.");
					return;
				}

				if (confirm("The certificate for \"" + exitpoint.name + "\" will be renewed now, which will affect your account's rate limits.  Continue if this is okay."))
				{
					try
					{
						this.showFullscreenLoader = true;
						const response = await ExecAPI("Configuration/ForceRenew", { forceRenewExitpointName: exitpoint.name });
						if (response.success)
							toaster.success("Renewal completed.");
						else
							toaster.error(response.error);
					}
					catch (ex)
					{
						console.log(ex);
						toaster.error(ex);
					}
					finally
					{
						this.showFullscreenLoader = false;
					}
				}
				else
					toaster.info("Force renewal was cancelled");
			},
			async uploadCertificate(e)
			{
				if (this.configurationChanged)
				{
					toaster.info("Please save the changes on this page, then try again to upload the certificate.");
					return;
				}

				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("Configuration/UploadCertificate", { exitpointName: e.exitpoint.name, certificateBase64: e.certificateBase64 });
					if (response.success)
					{
						toaster.success("Upload completed.");
						this.consumeConfigurationResponse(response);
					}
					else
						toaster.error(response.error);
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
				}
				finally
				{
					this.showFullscreenLoader = false;
				}
			},
			selectTab(tab)
			{
				this.selectedTab = tab;
				try
				{
					if (tab && tab.Name)
						localStorage.webProxySelectedTabName = tab.Name;
				}
				catch { }
				if (tab && tab.scrollTop)
					document.querySelector("#app").scrollTop = 0;
			},
			async testCloudflareDNS()
			{
				if (this.configurationChanged)
				{
					toaster.info("Please save the changes on this page, then try again.");
					return;
				}

				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("Configuration/TestCloudflareDNS");

					if (response.success)
						toaster.success("Test success.");
					else
						toaster.error(response.error);
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
				}
				finally
				{
					this.showFullscreenLoader = false;
				}
			},
			async uploadSettingsAndCertificatesClicked(selectedFileBase64)
			{
				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("Configuration/Import", { base64: selectedFileBase64 });
					if (response.success)
					{
						toaster.success("Import successful.");
						this.consumeConfigurationResponse(response);
					}
					else
						toaster.error(response.error);
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
				}
				finally
				{
					this.showFullscreenLoader = false;
				}
			},
			async forceGarbageCollection()
			{
				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("ServerStatus/GarbageCollect");

					if (response.success)
						toaster.success("Garbage Collection took " + response.milliseconds + "ms.");
					else
						toaster.error(response.error);
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
				}
				finally
				{
					this.showFullscreenLoader = false;
				}
			},
			async EnableServerGC()
			{
				if (this.configurationChanged)
				{
					toaster.info("Please save the changes on this page, then try again.");
					return;
				}
				if (!confirm("Warning: This operation will cause the server to be restarted."))
				{
					toaster.info("Operation was cancelled.");
					return;
				}

				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("Configuration/EnableServerGC");

					if (response.success)
					{
						toaster.success("Server is restarting");
						setTimeout(() =>
						{
							location.reload();
						}, 2000);
					}
					else
					{
						toaster.error(response.error);
						this.showFullscreenLoader = false;
					}
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
					this.showFullscreenLoader = false;
				}
			},
			async DisableServerGC()
			{
				if (this.configurationChanged)
				{
					toaster.info("Please save the changes on this page, then try again.");
					return;
				}
				if (!confirm("Warning: This operation will cause the server to be restarted."))
				{
					toaster.info("Operation was cancelled.");
					return;
				}

				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("Configuration/DisableServerGC");

					if (response.success)
					{
						toaster.success("Server is restarting");
						setTimeout(() =>
						{
							location.reload();
						}, 2000);
					}
					else
					{
						toaster.error(response.error);
						this.showFullscreenLoader = false;
					}
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
					this.showFullscreenLoader = false;
				}
			},
			async DeleteMemoryMax()
			{
				this.memoryMax = null;
				await this.SetMemoryMax(null);
			},
			async SetMemoryMax(MiB)
			{
				if (this.configurationChanged)
				{
					toaster.info("Please save the changes on this page, then try again.");
					return;
				}
				if (!confirm("Warning: This operation will cause the server to be restarted."))
				{
					toaster.info("Operation was cancelled.");
					return;
				}

				try
				{
					this.showFullscreenLoader = true;
					const response = await ExecAPI("Configuration/SetMemoryMaxMiB", { MiB });

					if (response.success)
					{
						toaster.success("Server is restarting");
						setTimeout(() =>
						{
							location.reload();
						}, 2000);
					}
					else
					{
						toaster.error(response.error);
						this.showFullscreenLoader = false;
					}
				}
				catch (ex)
				{
					console.log(ex);
					toaster.error(ex);
					this.showFullscreenLoader = false;
				}
			},
		},
		watch:
		{
			loading()
			{
				if (!this.loading)
				{
					this.$nextTick(() =>
					{
						store.recalcTabBarHeight();
					});
				}
			}
		}
	};
	let idCounter = 1;
	function FixEntrypoint(o)
	{
		if (!o.middlewares)
			o.middlewares = [];
		if (!o.tlsCipherSuiteSet)
			o.tlsCipherSuiteSet = store.tlsCipherSuiteSets[0];
		o.uniqueId = idCounter++; // uniqueId is a clientside-only field useful as a sticky identifier for each object.  uniqueId is not persisted by the server.
		return o;
	}
	function FixExitpoint(o)
	{
		if (!o.middlewares)
			o.middlewares = [];
		if (!o.type)
			o.type = store.exitpointTypes[0];
		if (typeof o.allowGenerateSelfSignedCertificate === "undefined")
			o.allowGenerateSelfSignedCertificate = true;
		if (typeof o.useConnectionKeepAlive === "undefined")
			o.useConnectionKeepAlive = true;
		if (!o.connectTimeoutSec)
			o.connectTimeoutSec = 10;
		if (!o.networkTimeoutSec)
			o.networkTimeoutSec = 15;
		o.uniqueId = idCounter++;
		return o;
	}
	function FixMiddleware(o)
	{
		if (!o.WhitelistedIpRanges)
			o.WhitelistedIpRanges = [];
		if (!o.AuthCredentials)
			o.AuthCredentials = [];
		if (!o.Type)
			o.Type = store.middlewareTypes[0];
		if (!o.ProxyHeaderBehavior)
			o.ProxyHeaderBehavior = store.proxyHeaderBehaviorOptions[0];
		if (!o.HttpHeaders)
			o.HttpHeaders = [];
		if (!o.HostnameSubstitutions)
			o.HostnameSubstitutions = [];
		if (!o.RegexReplacements)
			o.RegexReplacements = [];
		o.uniqueId = idCounter++;
		return o;
	}
	function FixProxyRoute(o)
	{
		if (!o.entrypointName)
			o.entrypointName = "NONE SELECTED";
		if (!o.exitpointName)
			o.exitpointName = "NONE SELECTED";
		o.uniqueId = idCounter++;
		return o;
	}
	function DeleteFromArray(item, array)
	{
		for (let i = 0; i < array.length; i++)
		{
			if (array[i] === item)
			{
				console.log("Deleting", item);
				array.splice(i, 1);
				i--;
			}
		}
	}
</script>

<style scoped>
	.hiddenFormToPreventAutofill
	{
		display: none;
	}

	.topBar
	{
		position: sticky;
		z-index: 2;
		top: 0px;
		border-bottom: 1px solid #888888;
		display: flex;
		align-items: center;
		min-height: 44px;
		box-sizing: border-box;
		padding: 0px 1em;
	}

		.topBar h1
		{
			display: inline;
			margin: 0px;
			font-size: 1.20em;
			flex: 0 1 auto;
			/*white-space: nowrap;
			text-overflow: ellipsis;
			overflow-x: hidden;*/
		}

	button.saveChanges
	{
		font-size: 20px;
		padding: 0px;
		margin-left: 10px;
		color: #FFFFFF;
		background-color: #FF0000;
		height: 44px;
		padding: 0px 12px;
		border-radius: 0px;
		border: none;
		flex: 0 0 auto;
	}

		button.saveChanges:hover
		{
			background-color: #FF3333;
		}

		button.saveChanges:focus
		{
			outline: 4px solid black;
		}

	.sidebarUnsavedChanges
	{
		position: fixed;
		top: 0;
		bottom: 0;
		width: 30px;
		background-color: rgba(255,0,0,0.0333);
	}

		.sidebarUnsavedChanges:before
		{
			content: "";
			position: absolute;
			top: 0;
			bottom: 0;
			left: 0;
			right: 0;
		}

		.sidebarUnsavedChanges.left
		{
			background: linear-gradient(-90deg, rgba(255,0,0,0.00) 33%, rgba(255,0,0,0.0333) 66%, rgba(255,0,0,0.00) 100%);
			left: -3px;
		}

		.sidebarUnsavedChanges.right
		{
			background: linear-gradient(90deg, rgba(255,0,0,0.00) 33%, rgba(255,0,0,0.0333) 66%, rgba(255,0,0,0.00) 100%);
			right: 10px;
		}

		.sidebarUnsavedChanges.left:before
		{
			background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='30' height='200' viewBox='0 0 30 200'%3E%3Ctext x='-200' y='16' transform='rotate(-90)' fill='%23FFAAAA' style='font-family: sans-serif; font-size: 16px;'%3EUnsaved Changes%3C/text%3E%3C/svg%3E");
		}

		.sidebarUnsavedChanges.right:before
		{
			background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='30' height='200' viewBox='0 0 30 200'%3E%3Ctext x='0' y='-16' transform='rotate(90)' fill='%23FFAAAA' style='font-family: sans-serif; font-size: 16px;'%3EUnsaved Changes%3C/text%3E%3C/svg%3E");
		}

	.tabBar
	{
		display: flex;
		flex-wrap: wrap;
		position: relative;
		z-index: 1;
		border-bottom: 1px solid #888888;
		box-sizing: border-box;
	}

		.tabBar .tab
		{
			height: 44px;
			line-height: 44px;
			padding: 0px 1em;
			cursor: pointer;
			user-select: none;
		}

			.tabBar .tab:hover
			{
				background-color: rgba(255,255,255,0.25);
			}

			.tabBar .tab.selectedTab
			{
				text-decoration: underline;
				background-color: rgba(255,255,255,0.15);
			}

				.tabBar .tab.selectedTab:hover
				{
					background-color: rgba(255,255,255,0.4);
				}

	.adminContent
	{
		margin: 0px 1em;
		padding-bottom: 1em;
	}

	.invisibleLine
	{
		height: 1px;
	}

	.password-container
	{
		align-self: stretch;
	}

	@media (min-width: 600px)
	{
		.adminBody
		{
		}

		.tabBar
		{
			position: fixed;
			top: 44px;
			z-index: 1;
			flex-direction: column;
			flex-wrap: nowrap;
			width: 125px;
			height: calc(100vh - 44px);
			overflow-y: auto;
			border-bottom: none;
		}

		.adminContent
		{
			padding-left: 125px;
		}

		.sidebarUnsavedChanges.left
		{
			left: 122px;
		}
	}

	@media (min-width: 700px)
	{
		.topBar h1
		{
			font-size: 1.5em;
		}
	}

	@media (min-width: 800px)
	{
		.topBar h1
		{
			font-size: 2em;
		}
	}
</style>

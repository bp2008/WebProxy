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
	<div v-if="loading">Loading...</div>
	<div class="adminBody" v-else>
		<loading v-model:active="showFullscreenLoader"
				 :can-cancel="false"
				 :is-full-page="true" />
		<div class="tabBar">
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
						<input type="text" v-model="store.errorTrackerSubmitUrl" autocomplete="off" />
						<div class="comment" v-if="store.showHelp">Optional submit URL for an ErrorTracker instance.</div>
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
					<ExitpointEditor v-for="exitpoint in store.exitpoints" :key="exitpoint.uniqueId" :exitpoint="exitpoint" @delete="deleteExitpoint(exitpoint)" @renew="renewCertificate(exitpoint)" />
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
				<p v-if="store.showHelp">The Proxy Routes list defines which Exitpoints are reachable from which Entrypoints.  In order for an Exitpoint to be reachable by clients, it must be bound to at least one Entrypoint via a Proxy Route.</p>
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
			</div>
			<div v-show="selectedTab.Name === 'All' || selectedTab.Name === 'Log'">
				<h2>Raw Settings.json</h2>
				<a href="/Configuration/GetRaw" target="_blank">Settings.json</a>
				<h2>Log Files</h2>
				<div v-for="logFile in store.logFiles">
					<a :href="'/Log/' + logFile.fileName" target="_blank">{{logFile.fileName}}</a> ({{logFile.size}})
				</div>
				<h2>Live Log File</h2>
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
	import LogReader from './components/LogReader.vue';
	import ExecAPI from './library/API';
	import store from '/src/library/store';
	import Loading from 'vue-loading-overlay';
	import { VueDraggableNext } from 'vue-draggable-next';

	export default {
		components: { EntrypointEditor, ExitpointEditor, MiddlewareEditor, ProxyRouteEditor, HostedUrlSummary, LogReader, Loading, draggable: VueDraggableNext },
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
				]
			};
		},
		created()
		{
			window.appRoot = this;
			this.selectedTab = this.tabs[1];
			this.getConfiguration();
		},
		computed:
		{
			currentJson()
			{
				return JSON.stringify({
					acmeAccountEmail: store.acmeAccountEmail,
					entrypoints: store.entrypoints,
					exitpoints: store.exitpoints,
					middlewares: store.middlewares,
					proxyRoutes: store.proxyRoutes,
					errorTrackerSubmitUrl: store.errorTrackerSubmitUrl
				});
			},
			configurationChanged()
			{
				return this.originalJson && this.currentJson !== this.originalJson;
			},
			appVersion()
			{
				return store ? store.appVersion : "";
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
						entrypoints: store.entrypoints,
						exitpoints: store.exitpoints,
						middlewares: store.middlewares,
						proxyRoutes: store.proxyRoutes,
						errorTrackerSubmitUrl: store.errorTrackerSubmitUrl
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
								setTimeout(() =>
								{
									location.href = bestOrigin;
								}, 2000);
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
				store.acmeAccountEmail = response.acmeAccountEmail;
				store.errorTrackerSubmitUrl = response.errorTrackerSubmitUrl;
				store.logFiles = response.logFiles;
				store.appVersion = response.appVersion;

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
			selectTab(tab)
			{
				this.selectedTab = tab;
				if (tab.scrollTop)
					document.querySelector("html").scrollTop = 0;
			}
		},
		watch:
		{
		}
	};
	let idCounter = 1;
	function FixEntrypoint(o)
	{
		if (!o.middlewares)
			o.middlewares = [];
		o.uniqueId = idCounter++; // uniqueId is a clientside-only field useful as a sticky identifier for each object.  uniqueId is not persisted by the server.
		return o;
	}
	function FixExitpoint(o)
	{
		if (!o.middlewares)
			o.middlewares = [];
		if (!o.type)
			o.type = store.exitpointTypes[0];
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
		/*background-color: #030839;*/
		background: #121212;
		background: radial-gradient(ellipse at left bottom, #121233 0%, #121212 100%);
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
		position: absolute;
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
			right: -3px;
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
		background-color: #121212;
		background: radial-gradient(ellipse at top right, #12122b 0%, #000000 100%);
		display: flex;
		flex-wrap: wrap;
		position: relative;
		z-index: 1;
		border-bottom: 1px solid #888888;
	}

		.tabBar .tab
		{
			color: #FFFFFF;
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

	.helpToggle
	{
		position: sticky;
		top: 50px;
		z-index: 1;
		background: #2E2E2E;
		color: rgba(255,255,255,0.77);
		font-size: 1.8em;
		display: inline-block;
		float: right;
		width: 44px;
		height: 44px;
		margin-top: 5px;
		margin-right: 5px;
		line-height: 42px;
		text-align: center;
		box-sizing: border-box;
		border: 1px solid #666666;
		border-radius: 4px;
		box-shadow: 0px 1px 4px 4px rgba(0,0,0,0.6);
		cursor: pointer;
	}

		.helpToggle:hover
		{
			background-color: #353535;
		}

		.helpToggle.enabled
		{
			background-color: #383838;
			color: rgba(0,255,0,1);
			border-color: rgba(0,255,0,0.66);
			font-weight: bold;
		}

			.helpToggle.enabled:hover
			{
				background-color: #404040;
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
		}

		.adminContent
		{
			padding-left: 125px;
		}

		.sidebarUnsavedChanges.left
		{
			left: 122px;
		}

		.tabBar
		{
			border-bottom: none;
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

<template>
	<div class="sidebarUnsavedChanges left" v-if="configurationChanged"></div>
	<div class="sidebarUnsavedChanges right" v-if="configurationChanged"></div>
	<div :class="{ topBar: true, configurationChanged: configurationChanged }">
		<h1>WebProxy Admin Console</h1>
		<button v-if="configurationChanged" class="saveChanges" @click="saveChanges">Save Changes</button>
	</div>
	<div v-if="loading">Loading...</div>
	<template v-else>
		<loading v-model:active="showFullscreenLoader"
				 :can-cancel="false"
				 :is-full-page="true" />
		<h2>Entrypoints</h2>
		<p>Entrypoints define how the web server listens for incoming network requests.</p>
		<draggable v-model="store.entrypoints" handle=".dragHandle">
			<EntrypointEditor v-for="entrypoint in store.entrypoints" :key="entrypoint.uniqueId" :entrypoint="entrypoint" @delete="deleteEntrypoint(entrypoint)" />
		</draggable>
		<div class="buttonBar">
			<button @click="addEntrypoint()">Add New Entrypoint</button>
		</div>
		<h2>Exitpoints</h2>
		<p>An Exitpoint is a web destination which a client wants to reach.  This Admin Console is an Exitpoint, but an Exitpoint could also be another web server.</p>
		<draggable v-model="store.entrypoints" handle=".dragHandle">
			<ExitpointEditor v-for="exitpoint in store.exitpoints" :key="exitpoint.uniqueId" :exitpoint="exitpoint" @delete="deleteExitpoint(exitpoint)" @renew="renewCertificate(exitpoint)" />
		</draggable>
		<div class="buttonBar">
			<button @click="addExitpoint()">Add New Exitpoint</button>
		</div>
		<h2>Middlewares</h2>
		<p>A Middleware is a module which applies additional logic to Entrypoints or Exitpoints.  A Middleware typically adds a usage constraint such as an authentication requirement or an IP Address whitelist, or can be used to automatically redirect unencrypted HTTP traffic to HTTPS.</p>
		<draggable v-model="store.entrypoints" handle=".dragHandle">
			<MiddlewareEditor v-for="middleware in store.middlewares" :key="middleware.uniqueId" :middleware="middleware" @delete="deleteMiddleware(middleware)" />
		</draggable>
		<div class="buttonBar">
			<button @click="addMiddleware()">Add New Middleware</button>
		</div>
		<h2>Proxy Routes</h2>
		<p>The Proxy Routes list defines which Exitpoints are reachable from which Entrypoints.  In order for an Exitpoint to be reachable by clients, it must be bound to at least one Entrypoint via a Proxy Route.</p>
		<draggable v-model="store.entrypoints" handle=".dragHandle">
			<ProxyRouteEditor v-for="proxyRoute in store.proxyRoutes" :key="proxyRoute.uniqueId" :proxyRoute="proxyRoute" @delete="deleteProxyRoute(proxyRoute)" />
		</draggable>
		<div class="buttonBar">
			<button @click="addProxyRoute()">Add New Proxy Route</button>
		</div>
		<h2>Raw Data</h2>
		<div class="code">
			{{JSON.stringify(store, null, 2)}}
		</div>
	</template>
</template>

<script>
	import EntrypointEditor from './components/EntrypointEditor.vue';
	import ExitpointEditor from './components/ExitpointEditor.vue';
	import MiddlewareEditor from './components/MiddlewareEditor.vue';
	import ProxyRouteEditor from './components/ProxyRouteEditor.vue';
	import ExecAPI from './library/API';
	import store from '/src/library/store';
	import Loading from 'vue-loading-overlay';
	import { VueDraggableNext } from 'vue-draggable-next';

	export default {
		components: { EntrypointEditor, ExitpointEditor, MiddlewareEditor, ProxyRouteEditor, Loading, draggable: VueDraggableNext },
		data()
		{
			return {
				store: store,
				loading: false,
				showFullscreenLoader: false,
				originalJson: null
			};
		},
		created()
		{
			window.appRoot = this;
			this.getConfiguration();
		},
		computed:
		{
			currentJson()
			{
				return JSON.stringify({
					entrypoints: store.entrypoints,
					exitpoints: store.exitpoints,
					middlewares: store.middlewares,
					proxyRoutes: store.proxyRoutes
				});
			},
			configurationChanged()
			{
				return this.originalJson && this.currentJson !== this.originalJson;
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
				store.exitpointTypes = response.exitpointTypes;
				store.middlewareTypes = response.middlewareTypes;

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
				DeleteFromArray(entrypoint, store.entrypoints);
			},
			deleteExitpoint(exitpoint)
			{
				DeleteFromArray(exitpoint, store.exitpoints);
			},
			deleteMiddleware(middleware)
			{
				DeleteFromArray(middleware, store.middlewares);
			},
			deleteProxyRoute(proxyRoute)
			{
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
			}
		},
		watch:
		{
			middlewares:
			{
				deep: true,
				handler()
				{
				}
			}
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
		o.uniqueId = idCounter++;
		return o;
	}
	function FixMiddleware(o)
	{
		if (!o.WhitelistedIpRanges)
			o.WhitelistedIpRanges = [];
		if (!o.AuthCredentials)
			o.AuthCredentials = {};
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
		top: 0px;
		background-color: #FFFFFF;
		border-bottom: 1px solid #AAAAAA;
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		min-height: 44px;
		box-sizing: border-box;
	}

	h1
	{
		display: inline;
		margin: 0px;
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
			left: 0;
		}

		.sidebarUnsavedChanges.right
		{
			background: linear-gradient(90deg, rgba(255,0,0,0.00) 33%, rgba(255,0,0,0.0333) 66%, rgba(255,0,0,0.00) 100%);
			right: 0;
		}

		.sidebarUnsavedChanges.left:before
		{
			background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='30' height='200' viewBox='0 0 30 200'%3E%3Ctext x='-200' y='16' transform='rotate(-90)' fill='%23FFAAAA' style='font-family: sans-serif; font-size: 16px;'%3EUnsaved Changes%3C/text%3E%3C/svg%3E");
		}

		.sidebarUnsavedChanges.right:before
		{
			background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='30' height='200' viewBox='0 0 30 200'%3E%3Ctext x='0' y='-16' transform='rotate(90)' fill='%23FFAAAA' style='font-family: sans-serif; font-size: 16px;'%3EUnsaved Changes%3C/text%3E%3C/svg%3E");
		}
</style>

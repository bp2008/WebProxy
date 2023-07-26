<template>
	<div class="primaryContainer">
		<div v-for="route in routes" class="route">
			<div v-for="origin in route.origins" class="origin">
				<span v-if="origin.wildcard">{{origin.href}}</span>
				<a v-else-if="!origin.wildcard" :href="origin.href">{{origin.href}}</a>
			</div>
			<div class="destination">
				&#8594 {{route.destination}}
			</div>
			<div class="constraint" v-for="constraint in route.constraints">
				{{constraint}}
			</div>
		</div>
	</div>
</template>

<script>
	import store from '/src/library/store';
	import { splitExitpointHostList } from '/src/library/Util';

	export default {
		components: {},
		props: {
		},
		data()
		{
			return {
			};
		},
		created()
		{
		},
		computed:
		{
			routes()
			{
				let arr = [];
				for (let i = 0; i < store.proxyRoutes.length; i++)
				{
					let r = store.proxyRoutes[i];
					if (!r)
						continue;
					let entrypoint = this.getEntrypoint(r.entrypointName);
					let exitpoint = this.getExitpoint(r.exitpointName);
					if (!entrypoint || !exitpoint)
						continue;
					if (!exitpoint.host)
						continue;

					arr.push({
						origins: this.getOrigins(entrypoint, exitpoint),
						destination: this.getDestination(exitpoint),
						constraints: this.getConstraints(entrypoint, exitpoint)
					});
				}
				return arr;
			}
		},
		methods:
		{
			getEntrypoint(name)
			{
				for (let i = 0; i < store.entrypoints.length; i++)
					if (store.entrypoints[i].name === name)
						return store.entrypoints[i];
				return null;
			},
			getExitpoint(name)
			{
				for (let i = 0; i < store.exitpoints.length; i++)
					if (store.exitpoints[i].name === name)
						return store.exitpoints[i];
				return null;
			},
			buildHref(isHttp, port, host)
			{
				if (port > 0 && port < 65536)
				{
					let isDefaultPort = (isHttp && port === 80) || (!isHttp && port === 443);
					return "http" + (isHttp ? "" : "s") + "://" + host + (isDefaultPort ? "" : (":" + port));
				}
				return null;
			},
			getOrigins(entrypoint, exitpoint)
			{
				let origins = [];
				let hosts = splitExitpointHostList(exitpoint.host);
				for (let n = 0; n < hosts.length; n++)
				{
					let h = hosts[n];
					let wildcard = h.indexOf('*') > -1;
					let urls = [
						this.buildHref(true, entrypoint.httpPort, h),
						this.buildHref(false, entrypoint.httpsPort, h)
					].filter(Boolean);
					for (let x = 0; x < urls.length; x++)
					{
						origins.push({
							wildcard: wildcard,
							href: urls[x],
						});
					}
				}
				return origins;
			},
			getDestination(exitpoint)
			{
				let destination = "unknown";
				if (exitpoint.type === "AdminConsole")
					destination = "Admin Console";
				else if (exitpoint.type === "WebProxy")
				{
					destination = exitpoint.destinationOrigin;
					if (exitpoint.destinationHostHeader)
						destination += " [Host: " + exitpoint.destinationHostHeader + "]";
				}
				return destination;
			},
			getConstraints(entrypoint, exitpoint)
			{
				let constraints = {};

				let middlewareNames = {};
				for (let i = 0; i < entrypoint.middlewares.length; i++)
					middlewareNames[entrypoint.middlewares[i]] = true;
				for (let i = 0; i < exitpoint.middlewares.length; i++)
					middlewareNames[exitpoint.middlewares[i]] = true;

				for (let middlewareName in middlewareNames)
				{
					for (let i = 0; i < store.middlewares.length; i++)
					{
						let m = store.middlewares[i];
						if (m.Id === middlewareName)
						{
							if (m.Type === "IPWhitelist")
								constraints["IP Whitelist"] = true;
							else if (m.Type === "HttpDigestAuth")
								constraints["HTTP Digest Authentication"] = true;
							else if (m.Type === "AddHttpHeaderToResponse")
								constraints["Add Header: " + m.HttpHeader] = true;
							else if (m.Type === "AddProxyServerTiming")
								constraints["Add Header: Server-Timing"] = true;
							else if (m.Type === "RedirectHttpToHttps")
								constraints["Force TLS"] = true;

							break;
						}
					}
				}

				let arr = [];
				for (let c in constraints)
					arr.push(c);
				return arr;
			}
		},
		watch:
		{
		}
	};
</script>

<style scoped>
	.route
	{
		margin-top: 1em;
	}

		.route:first-child
		{
			margin-top: 0em;
		}

	.origin
	{
		margin-top: 2px;
	}

	.destination
	{
		margin-top: 2px;
		margin-left: 1em;
	}

	.constraint
	{
		margin-top: 2px;
		margin-left: 2.25em;
		color: #ff6a00;
	}
</style>
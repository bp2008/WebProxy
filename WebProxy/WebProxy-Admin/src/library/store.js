import { reactive, watch, computed } from 'vue';

const store = reactive({
	currentTheme: "",
	darkTheme: computed(() => store.currentTheme.indexOf('dark') > -1),
	themeList: ["dark", "dark2", "light"],
	showHelp: false,
	exitpointTypes: [],
	middlewareTypes: [],
	proxyHeaderBehaviorOptions: [],
	proxyHeaderBehaviorOptionsDescriptions: {},
	tlsCipherSuiteSets: [],
	tlsCipherSuiteSetDescriptions: {},
	tlsCipherSuitesPolicySupported: false,
	entrypoints: [],
	exitpoints: [],
	middlewares: [],
	proxyRoutes: [],
	acmeAccountEmail: "",
	errorTrackerSubmitUrl: "",
	cloudflareApiToken: "",
	verboseWebServerLogs: false,
	logFiles: [],
	gcModeServer: false,
	serverMaxConnectionCount: 0,
	appVersion: "",
	windowWidth: -1,
	windowHeight: -1,
	windowSize: computed(() => store.windowWidth + "x" + store.windowHeight),
	mobileLayout: false,
	tabBarHeight: 0,
	recalcTabBarHeight: recalcTabBarHeight,
	expansionState: {},
});

// Mobile Layout
watch(() => store.mobileLayout, () =>
{
	recalcTabBarHeight();
})

let mediaQuery_w600px = window.matchMedia('(min-width: 600px)');
store.mobileLayout = !mediaQuery_w600px.matches;

mediaQuery_w600px.addEventListener('change', e =>
{
	store.mobileLayout = !mediaQuery_w600px.matches;
})

// Window Size
store.windowWidth = window.innerWidth;
store.windowHeight = window.innerHeight;
window.addEventListener('resize', () =>
{
	store.windowWidth = window.innerWidth;
	store.windowHeight = window.innerHeight;
	recalcTabBarHeight();
});

function recalcTabBarHeight()
{
	if (store.mobileLayout)
	{
		let tabBar = document.getElementById('tabBar');
		if (tabBar)
			store.tabBarHeight = tabBar.clientHeight;
		else
			store.tabBarHeight = 0;
	}
	else
		store.tabBarHeight = 0;
}

// Theme
watch(() => store.currentTheme, () =>
{
	localStorage.setItem(themeSettingKey, store.currentTheme);
	document.querySelector('#app').classList = "theme-" + store.currentTheme;
})

const themeSettingKey = "theme-preference";

let mediaQuery_theme = window.matchMedia('(prefers-color-scheme: dark)');
if (localStorage.getItem(themeSettingKey))
{
	let t = localStorage.getItem(themeSettingKey);
	for (let i = 0; i < store.themeList.length; i++)
		if (store.themeList[i] === t)
			store.currentTheme = t;
}
else
	store.currentTheme = mediaQuery_theme.matches ? 'dark' : 'light';

if (!store.currentTheme)
	store.currentTheme = 'dark';

mediaQuery_theme.addEventListener('change', e =>
{
	store.currentTheme = e.matches ? 'dark' : 'light';
})

// Primary Container Expansion State (Load / Save)
const expansionStateKey = "wp_expansion_state";
function LoadExpansionState()
{
	try
	{
		let expansionState = localStorage.getItem(expansionStateKey);
		if (typeof expansionState !== "string")
			expansionState = "{}";
		let expansionMap = JSON.parse(expansionState);
		if (typeof expansionMap === "object")
			return expansionMap;
	}
	catch (ex)
	{
		console.log(ex);
	}
	return {};
}
store.expansionState = LoadExpansionState();
watch(() => store.expansionState, () =>
{
	if (store.entrypoints.length > 0)
	{
		// data is loaded, so we can delete stale expansion states
		let validKeys = {};
		for (let i = 0; i < store.entrypoints.length; i++)
			validKeys["Entrypoint_" + store.entrypoints[i].name] = true;
		for (let i = 0; i < store.exitpoints.length; i++)
			validKeys["Exitpoint_" + store.exitpoints[i].name] = true;
		for (let i = 0; i < store.middlewares.length; i++)
			validKeys["Middleware_" + store.middlewares[i].Id] = true;

		for (let key in store.expansionState)
		{
			if (store.expansionState.hasOwnProperty(key))
			{
				if (!validKeys[key])
					delete store.expansionState[key];
			}
		}
	}
	try
	{
		localStorage.setItem(expansionStateKey, JSON.stringify(store.expansionState));
	}
	catch (ex)
	{
		console.error(ex);
	}
}, { deep: true });

// Export
window.appStore = store; // Handle for debugging
export default store;
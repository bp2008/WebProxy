import { reactive, watch } from 'vue';

const store = reactive({
	currentTheme: "",
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
	appVersion: "",
	windowWidth: -1,
	windowHeight: -1,
	mobileLayout: false,
	tabBarHeight: 0,
	recalcTabBarHeight: recalcTabBarHeight
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

// Export
window.appStore = store; // Handle for debugging
export default store;
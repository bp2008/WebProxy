import { reactive, watch } from 'vue';

const store = reactive({
	currentTheme: "",
	themeList: ["dark", "dark2", "light"],
	showHelp: false,
	exitpointTypes: [],
	middlewareTypes: [],
	proxyHeaderBehaviorOptions: [],
	proxyHeaderBehaviorOptionsDescriptions: {},
	entrypoints: [],
	exitpoints: [],
	middlewares: [],
	proxyRoutes: [],
	acmeAccountEmail: "",
	errorTrackerSubmitUrl: "",
	logFiles: [],
	appVersion: "",
	windowWidth: -1,
	windowHeight: -1
});

// Window Size
store.windowWidth = window.innerWidth;
store.windowHeight = window.innerHeight;
window.addEventListener('resize', () =>
{
	store.windowWidth = window.innerWidth;
	store.windowHeight = window.innerHeight;
});

// Theme
watch(() => store.currentTheme, () =>
{
	localStorage.setItem(themeSettingKey, store.currentTheme);
	document.querySelector('#app').classList = "theme-" + store.currentTheme;
})

const themeSettingKey = "theme-preference";

let mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
if (localStorage.getItem(themeSettingKey))
{
	let t = localStorage.getItem(themeSettingKey);
	for (let i = 0; i < store.themeList.length; i++)
		if (store.themeList[i] === t)
			store.currentTheme = t;
}
else
	store.currentTheme = mediaQuery.matches ? 'dark' : 'light';

if (!store.currentTheme)
	store.currentTheme = 'dark';

mediaQuery.addEventListener('change', e =>
{
	store.currentTheme = e.matches ? 'dark' : 'light';
})

// Export
window.appStore = store; // Handle for debugging
export default store;
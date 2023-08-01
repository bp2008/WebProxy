import { reactive } from 'vue';

const store = reactive({
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

store.windowWidth = window.innerWidth;
store.windowHeight = window.innerHeight;
window.addEventListener('resize', () =>
{
	store.windowWidth = window.innerWidth;
	store.windowHeight = window.innerHeight;
});

window.appStore = store; // Handle for debugging
export default store;
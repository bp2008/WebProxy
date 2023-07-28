import { reactive } from 'vue';

const store = reactive({
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
	logFiles: []
});
window.appStore = store; // Handle for debugging
export default store;
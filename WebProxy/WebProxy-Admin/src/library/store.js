import { reactive } from 'vue';

const store = reactive({
	exitpointTypes: [],
	middlewareTypes: [],
	entrypoints: [],
	exitpoints: [],
	middlewares: [],
	proxyRoutes: [],
});
window.appStore = store; // Handle for debugging
export default store;
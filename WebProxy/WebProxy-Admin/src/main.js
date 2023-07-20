import { createApp } from 'vue'
import 'vue-loading-overlay/dist/css/index.css';
import './style.css'
import App from './App.vue'
import 'vue3-toastify/dist/index.css';
import ToasterHelper from './library/ToasterHelper'

window.toaster = new ToasterHelper();

createApp(App).mount('#app')

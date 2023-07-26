import { createApp } from 'vue'
import 'vue-loading-overlay/dist/css/index.css';
import './style.css'
import App from './App.vue'
import 'vue3-toastify/dist/index.css';
import ToasterHelper from './library/ToasterHelper'

window.toaster = new ToasterHelper();

window.onerror = function (msg, url, line, charIdx)
{
	try
	{
		var errStr = "An unexpected error has occurred in " + location.pathname + ".\n\n" + msg + "\nat " + url + " [" + line + ":" + charIdx + "]\n\n" + navigator.userAgent;
		try
		{
			if (toaster)
			{
				toaster.Error(errStr, 600000);
				return;
			}
		}
		catch (ex)
		{
			console.error("Failure to report error via toast", ex);
		}
		alert(errStr);
	}
	catch (ex)
	{
		alert(ex);
	}
};

window.addEventListener('unhandledrejection', function (event)
{
	try
	{
		var errStr = "An unhandled promise rejection has occurred in " + event.promise + ". ";
		if (event.reason)
		{
			if (event.reason.message)
				errStr += " " + event.reason.message + "\n";
			else
				errStr += " " + event.reason + "\n";

			if (event.reason.stack)
				errStr += " " + event.reason.stack + "\n";
		}
		errStr += "\n" + navigator.userAgent;
		try
		{
			if (toaster)
			{
				toaster.Error(errStr, 600000);
				return;
			}
		}
		catch (ex)
		{
			console.error("Failure to report error via toast", ex);
		}
		alert(errStr);
	}
	catch (ex)
	{
		alert(ex);
	}
});

createApp(App).mount('#app')

import { toast } from 'vue3-toastify';
import * as Util from './Util.js';

export default function ToasterHelper()
{
	this.error = this.Error = function (text, duration)
	{
		if (!duration)
			duration = 15000;
		return makeToast('error', text, duration);
	};
	this.warning = this.Warning = function (text, duration)
	{
		return makeToast('warning', text, duration);
	};
	this.info = this.Info = function (text, duration)
	{
		return makeToast('info', text, duration);
	};
	this.success = this.Success = function (text, duration)
	{
		return makeToast('success', text, duration);
	};
	this.default = this.Default = function (text, duration)
	{
		return makeToast('default', text, duration);
	};
	function makeToast(type, message, duration)
	{
		if (!duration)
			duration = 3000;
		if (typeof message === "object" && typeof message.message === "string" && typeof message.stack === "string")
		{
			console.error(type + " toast", message);
			message = message.message + ": " + message.stack;
		}
		else if (typeof message === "object" && typeof message.name === "string" && typeof message.message === "string" && typeof message.code === "number")
		{
			message = message.name + " (code " + message.code + "): " + message.message, message;
			console.error(type + " toast", message);
		}
		else
		{
			if (type === "error")
				console.error(type + " toast: ", message);
			else
				console.log(type + " toast: ", message);
		}
		let options = {
			type: type,
			theme: "light",
			autoClose: duration,
			position: toast.POSITION.BOTTOM_RIGHT
		};
		let id = toast(message, options);;
		return { close: function () { remove.done(id); } };
	}

	this.promise = function (promise, messages)
	{
		let options = {
			theme: "light",
			position: toast.POSITION.BOTTOM_RIGHT
		};
		return toast.promise(promise, messages, options);
	}

	this.loading = function (message)
	{
		let options = {
			theme: "light",
			position: toast.POSITION.BOTTOM_RIGHT
		};
		let id = toast.loading(message, options);
		return { close: function () { toast.remove(id); } };
	}
}
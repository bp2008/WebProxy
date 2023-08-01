
/**
 * Executes an API call to the specified method, using the specified arguments.  Returns a promise which resolves with any graceful response from the server.  Rejects if an error occurred that prevents the normal functioning of the API (e.g. the server was unreachable or returned an entirely unexpected response such as HTTP 500).
 * @param {String} method Server route, e.g. "Auth/Login"
 * @param {Object} args arguments
 * @returns {Promise} A promise which resolves with any graceful response from the server.
 */
export default function ExecAPI(method, args)
{
	if (!args)
		args = {};
	if (!args.sid)
		args.sid = window.myApp.$store.getters.sid;
	return fetch(appContext.appPath + method, {
		method: 'POST',
		headers: {
			'Accept': 'application/json',
			'Content-Type': 'application/json'
		},
		body: JSON.stringify(args)
	})
		.then(response =>
		{
			if (response.status === 200)
				return response.json();
			else if (response.status === 403)
			{
				if (!GetRouteMatched(window.myApp.$route, route => route.name === "loginLayout"))
				{
					toaster.error("Your session was lost.");
					CloseAllDialogs();
					window.myApp.$store.commit("SessionLost");
					window.myApp.$router.push({ name: "login", query: { path: window.myApp.$route.fullPath } });
				}
				return new Promise((resolve, reject) => { });
			}
			else if (response.status === 418)
			{
				if (!GetRouteMatched(window.myApp.$route, route => route.name === "loginLayout"))
				{
					toaster.error("Your session does not have sufficient privilege to access the requested resource.");
					CloseAllDialogs();
					window.myApp.$router.replace({ name: "login" });
				}
				return new Promise((resolve, reject) => { });
			}
			else
			{
				let errText = "API response was " + response.status + " " + response.statusText;
				console.error(errText);
				logResponseBodyHtmlAsError(response);
				return Promise.reject(new ApiError(errText));
			}
		})
		.then(data =>
		{
			return Promise.resolve(data);
		})
		.catch(err =>
		{
			console.error(err);
			return Promise.reject(err);
		});
}
function logResponseBodyHtmlAsError(response)
{
	response.text()
		.then(data =>
		{
			console.log(HTMLToText(data).trim());
		})
		.catch(err =>
		{
			console.error("Unable to retrieve response body", err);
		});
}
export class ApiError extends Error
{
	constructor(message, data)
	{
		super(message);
		this.name = "ApiError";
		this.data = data;
	}
}
/**
 * Splits the given string on ',' and ' ', removing empty entries.
 * @param {String} str String to split.
 */
export function splitExitpointHostList(str)
{
	return str.split(/,| /).filter(Boolean);
}
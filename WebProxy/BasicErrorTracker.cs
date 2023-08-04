using BPUtil;
using ErrorTrackerClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebProxy
{
	public static class BasicErrorTracker
	{
		private static SimpleThreadPool errorSubmissionThreadPool = new SimpleThreadPool("BasicErrorTracker Submissions", 0, 8, logErrorAction: (ex, additionalInfo) =>
		{
			// An error occurred during submission thread execution. This is likely to fail too.
			Event e = new Event(EventType.Error, "Exception during error submission", additionalInfo + ": " + ex.ToHierarchicalString());
			client.SubmitEvent(e);
		});

		private static ErrorClient client = new ErrorClient(JsonConvert.SerializeObject, () => WebProxyService.MakeLocalSettingsReference().errorTrackerSubmitUrl, () => Path.Combine(Globals.WritableDirectoryBase, "ErrorTrackerSave"), true);
		public static bool initialized = false;
		/// <summary>
		/// Call this just to ensure that the ErrorClient is constructed so it can begin sending cached events.
		/// </summary>
		public static void Initialize()
		{
			initialized = true;
		}

		/// <summary>
		/// Gets a value indicating if it is okay to submit events.
		/// </summary>
		private static bool OK
		{
			get
			{
				return !string.IsNullOrWhiteSpace(WebProxyService.MakeLocalSettingsReference().errorTrackerSubmitUrl);
			}
		}

		/// <summary>
		/// Sends an Error event to the configured ErrorTracker.
		/// </summary>
		/// <param name="exception">Exception object.</param>
		public static void GenericError(Exception exception)
		{
			GenericMessage(EventType.Error, exception.ToHierarchicalString());
		}
		/// <summary>
		/// Sends an Error event to the configured ErrorTracker.
		/// </summary>
		/// <param name="message">Message text.</param>
		public static void GenericError(string message)
		{
			GenericMessage(EventType.Error, message);
		}
		/// <summary>
		/// Sends an Info event to the configured ErrorTracker.
		/// </summary>
		/// <param name="message">Message text.</param>
		public static void GenericInfo(string message)
		{
			GenericMessage(EventType.Info, message);
		}
		/// <summary>
		/// Sends a Debug event to the configured ErrorTracker.
		/// </summary>
		/// <param name="message">Message text.</param>
		public static void GenericDebug(string message)
		{
			GenericMessage(EventType.Debug, message);
		}
		private static void GenericMessage(EventType eventType, string message)
		{
			if (!OK)
				return;
			Event e = new Event(eventType, "WebProxy Generic " + eventType.ToString(), message);

			Process self = Process.GetCurrentProcess();
			TimeSpan uptime = DateTime.Now - self.StartTime;
			e.SetTag("Server", Environment.MachineName);
			e.SetTag("Server Time", DateTime.Now.ToString());
			e.SetTag("Uptime", uptime.ToString());
			e.SetTag("Version", Globals.AssemblyVersion.ToString());
			errorSubmissionThreadPool.Enqueue(() => client.SubmitEvent(e));
		}
	}
}
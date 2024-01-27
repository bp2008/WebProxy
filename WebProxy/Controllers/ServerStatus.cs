using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using BPUtil.SimpleHttp.WebSockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebProxy.LetsEncrypt;

namespace WebProxy.Controllers
{
	public class ServerStatus : AdminConsoleControllerBase
	{
		[RequiresHttpMethod("GET")]
		public ActionResult GetServerStatusStream()
		{
			WebSocket socket = new WebSocket(Context.httpProcessor);

			socket.SendTimeout = socket.ReceiveTimeout = 20000;

			EventWaitHandle ewhDisconnect = new EventWaitHandle(false, EventResetMode.ManualReset);
			bool isConnected = true;
			socket.StartReading(frame =>
			{
				// Ignore anything sent by the client on this WebSocket.
			}
				, closeFrame =>
				{
					isConnected = false;
					ewhDisconnect.Set();
				});

			CountdownStopwatch pingTimer = CountdownStopwatch.StartNew(TimeSpan.FromSeconds(10));
			string lastJson = null;
			Stopwatch swCpuTimer = Stopwatch.StartNew();
			CountdownStopwatch cd60 = new CountdownStopwatch(TimeSpan.FromSeconds(60)); // Just construct, do not start.
			CountdownStopwatch cd10 = new CountdownStopwatch(TimeSpan.FromSeconds(10));
			double lastCpuTime = 0;
			long lastCpuMeasurementAt = 0;
			ulong ramSize = 0;
			int coreCount = 1;
			int coreCountDigits = 1;
			int minThreads = 0;
			int maxThreads = 0;
			int availableThreads = 0;
			int minCompletionPortThreads = 0;
			int maxCompletionPortThreads = 0;
			int availableCompletionPortThreads = 0;
			while (isConnected && !WebProxyService.abort)
			{
				if (CheckCooldown(cd60))
				{
					ramSize = GetRamSize();
					coreCount = Environment.ProcessorCount;
					coreCountDigits = coreCount.ToString().Length;
				}
				if (CheckCooldown(cd10))
				{
					ThreadPool.GetMinThreads(out minThreads, out minCompletionPortThreads);
					ThreadPool.GetMaxThreads(out maxThreads, out maxCompletionPortThreads);
				}
				ThreadPool.GetAvailableThreads(out availableThreads, out availableCompletionPortThreads);
				Process me = Process.GetCurrentProcess();
				TimeSpan cpuTime = me.TotalProcessorTime;
				double nowCpuTime = cpuTime.TotalMilliseconds;
				long timeNow = swCpuTimer.ElapsedMilliseconds;
				long timeElapsed = timeNow - lastCpuMeasurementAt;
				lastCpuMeasurementAt = timeNow;
				string cpuCoreUsagePercent;
				if (lastCpuTime == 0 || timeElapsed <= 0)
					cpuCoreUsagePercent = "0.0";
				else
				{
					double cpuUsedLastInterval = nowCpuTime - lastCpuTime;
					cpuUsedLastInterval = (cpuUsedLastInterval / timeElapsed);
					cpuCoreUsagePercent = cpuUsedLastInterval.ToString("0.00").PadLeft(3 + coreCountDigits, ' ');
				}
				GCMemoryInfo gcMem = GC.GetGCMemoryInfo();
				lastCpuTime = nowCpuTime;
				string json = JsonConvert.SerializeObject(new
				{
					serverIsUnderHeavyLoad = WebProxyService.WebServerIsUnderHeavyLoad,
					serverOpenConnectionCount = WebProxyService.WebServerOpenConnectionCount,
					serverMaxConnectionCount = WebProxyService.MakeLocalSettingsReference().serverMaxConnectionCount,
					mem_privateMemorySize = me.PrivateMemorySize64,
					mem_workingSet = me.WorkingSet64,
					cpu_processorTime = TimeUtil.ToDHMS(cpuTime),
					cpu_coreUsagePercent = cpuCoreUsagePercent,
					cpu_coreCount = coreCount,
					connectionsServed = WebProxyService.TotalConnectionsServed,
					requestsServed = WebProxyService.TotalRequestsServed,
					gc = gcMem,
					ramSize = ramSize,
					isServerGc = GCSettings.IsServerGC,
					minThreads,
					maxThreads,
					availableThreads,
					minCompletionPortThreads,
					maxCompletionPortThreads,
					availableCompletionPortThreads,
					activeConnections = Context.Server.GetActiveHttpProcessors()
						.Select(p => p.GetSummary())
						.ToArray(),
				});
				if (json != lastJson)
					socket.Send(json);
				lastJson = json;
				if (isConnected && !WebProxyService.abort && pingTimer.Finished)
				{
					socket.SendPing();
					pingTimer.Restart();
				}
				if (isConnected && !WebProxyService.abort)
				{
					if (ewhDisconnect.WaitOne(500))
						break;
				}
			}
			if (isConnected)
				socket.Close();

			return Empty();
		}
		private bool CheckCooldown(CountdownStopwatch cd)
		{
			if (!cd.IsRunning || cd.Finished)
			{
				cd.Restart();
				return true;
			}
			return false;
		}
		private ulong GetRamSize()
		{
			try
			{
				return Ram.GetRamSize();
			}
			catch
			{
				return 0;
			}
		}
		public ActionResult GarbageCollect()
		{
			Stopwatch sw = Stopwatch.StartNew();

			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

			return ApiSuccessNoAutocomplete(new GCResponse(sw.ElapsedMilliseconds));
		}
		/// <summary>
		/// Gets the proxyOptions instance currently associated with the given ConnectionID.  Will return an API error if the Connection is not found, or null if the connection does not currently have a proxyOptions instance.
		/// </summary>
		/// <returns></returns>
		public async Task<ActionResult> GetProxyOptions()
		{
			ProxyOptionsRequest request = await ParseRequest<ProxyOptionsRequest>().ConfigureAwait(false);
			HttpProcessor p = Context.Server.GetActiveHttpProcessors().FirstOrDefault(p => p.ConnectionID == request.connectionID);
			if (p == null)
				return ApiError("Connection ID " + request.connectionID + " was not found.");

			BPUtil.SimpleHttp.Client.ProxyOptions options = p.proxyOptions;
			string json = null;
			if (options != null)
			{
				JsonSerializerSettings settings = new JsonSerializerSettings
				{
					Converters = { new Utility.StringBuilderConverter() },
				};
				ProxyOptionsResponse response = new ProxyOptionsResponse(options);
				json = JsonConvert.SerializeObject(response, Formatting.Indented, settings);
				return new StringResult(json, "application/json") { ResponseStatus = "418 Success But Prevent Autocomplete" };
			}
			else
				return ApiError("Connection ID " + request.connectionID + " does not have ProxyOptions.");
		}
	}
	public class GCResponse : ApiResponseBase
	{
		public long milliseconds;
		public GCResponse(long milliseconds) : base(true)
		{
			this.milliseconds = milliseconds;
		}
	}
	public class ProxyOptionsRequest
	{
		/// <summary>
		/// ID of the connection. Connection IDs are simple auto-incremented numbers starting with 1 at the start of the server process.
		/// </summary>
		public long connectionID;
	}
	public class ProxyOptionsResponse : ApiResponseBase
	{
		public BPUtil.SimpleHttp.Client.ProxyOptions proxyOptions;
		public ProxyOptionsResponse(BPUtil.SimpleHttp.Client.ProxyOptions proxyOptions) : base(true)
		{
			this.proxyOptions = proxyOptions;
		}
	}
}

using BPUtil;
using BPUtil.MVC;
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
		public ActionResult GetServerStatusStream()
		{
			WebSocket socket = new WebSocket(Context.httpProcessor);

			Context.httpProcessor.tcpClient.ReceiveTimeout = Context.httpProcessor.tcpClient.SendTimeout = 20000;

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
			CountdownStopwatch reReadRamSize = CountdownStopwatch.StartNew(TimeSpan.FromMinutes(1));
			double lastCpuTime = 0;
			long lastCpuMeasurementAt = 0;
			ulong ramSize = GetRamSize();
			while (isConnected && !WebProxyService.abort)
			{
				if (reReadRamSize.Finished)
				{
					ramSize = GetRamSize();
					reReadRamSize.Restart();
				}
				Process me = Process.GetCurrentProcess();
				TimeSpan cpuTime = me.TotalProcessorTime;
				double nowCpuTime = cpuTime.TotalMilliseconds;
				long timeNow = swCpuTimer.ElapsedMilliseconds;
				long timeElapsed = timeNow - lastCpuMeasurementAt;
				lastCpuMeasurementAt = timeNow;
				string cpuUsagePercent;
				if (lastCpuTime == 0 || timeElapsed <= 0)
					cpuUsagePercent = "0.0";
				else
				{
					double cpuUsedLastInterval = nowCpuTime - lastCpuTime;
					cpuUsedLastInterval = (cpuUsedLastInterval / timeElapsed) * 100.0;
					cpuUsagePercent = cpuUsedLastInterval.ToString("0.0");
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
					cpu_processorTime = cpuTime.ToString(),
					cpu_coreUsagePercent = cpuUsagePercent,
					connectionsServed = WebProxyService.TotalConnectionsServed,
					requestsServed = WebProxyService.TotalRequestsServed,
					gc = gcMem,
					ramSize = ramSize
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
	}
	public class GCResponse : ApiResponseBase
	{
		public long milliseconds;
		public GCResponse(long milliseconds) : base(true)
		{
			this.milliseconds = milliseconds;
		}
	}
}

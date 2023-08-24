using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp.WebSockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebProxy.LetsEncrypt;

namespace WebProxy.Controllers
{
	public class Log : AdminConsoleControllerBase
	{
		public ActionResult Index(string logFileName)
		{
			string filePath = Globals.WritableDirectoryBase + "Logs/" + logFileName;
			if (!logFileName.Contains('/') && !logFileName.Contains('\\'))
			{
				if (File.Exists(filePath))
					return PlainText(File.ReadAllText(filePath));

				filePath = Globals.WritableDirectoryBase + "Logs/" + Globals.AssemblyName + "_" + logFileName;
				if (File.Exists(filePath))
					return PlainText(File.ReadAllText(filePath));

				filePath = Globals.WritableDirectoryBase + "Logs/" + Globals.AssemblyName + "_" + logFileName + ".txt";
				if (File.Exists(filePath))
					return PlainText(File.ReadAllText(filePath));
			}

			return this.StatusCode("404 Not Found");
		}
		public ActionResult GetLogData()
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

			StringBuilder sb = new StringBuilder();
			using (StreamingLogReader2 reader = new StreamingLogReader2(257))
			{
				CountdownStopwatch pingTimer = CountdownStopwatch.StartNew(TimeSpan.FromSeconds(15));
				while (isConnected && !WebProxyService.abort)
				{
					while (isConnected && !WebProxyService.abort && pingTimer.RemainingMilliseconds > 0)
					{
						if (ewhDisconnect.WaitOne(500))
							break;

						// Read from the log
						reader.ReadInto(sb);
						if (sb.Length > 0)
						{
							socket.Send(sb.ToString());
							sb.Clear();
						}
					}
					if (isConnected && !WebProxyService.abort)
					{
						socket.SendPing();
						pingTimer.Restart();
					}
				}
			}

			if (isConnected)
				socket.Close();

			return Empty();
		}
	}
}

using Spectre.Console;
using System.Diagnostics;
using BPUtil;

namespace WebProxyStressTester
{
	internal class Program
	{
		static long activeConnections = 0;
		static long totalRequests = 0;
		static long totalErrors = 0;
		static long lastRequestTimeMs = 0;

		static volatile bool abort = false;
		//static int NUM_CONNECTIONS = (int)(Environment.ProcessorCount * 1.25);
		static int NUM_CONNECTIONS = 24;
		static string[] urls = new string[] { "http://localhost:8080/", "http://localhost:8080/src/themes.scss", "http://localhost:8080/Log/Test" };
		//static string[] urls = new string[] { "http://localhost:80/src/themes.scss" };
		//static string[] urls = new string[] { "http://localhost:80/", "http://localhost:80/src/themes.scss", "http://localhost:80/Log/Test" };
		static object consoleLock = new object();
		static void Main(string[] args)
		{
			Stopwatch sw = Stopwatch.StartNew();

			RateOfChange requestRate = new RateOfChange(0);

			AnsiConsole.Status().Start("Starting Up", ctx =>
			{
				if (System.Diagnostics.Debugger.IsAttached)
					Thread.Sleep(250); // Helps start up faster, believe it or not.
				for (int i = 0; i < NUM_CONNECTIONS; i++)
				{
					StartLoadThread(i);
				}
			});

			Table table = new Table().LeftAligned();
			AnsiConsole.Live(table)
				.Start(ctx =>
				{
					try
					{
						table.AddColumn("[blue]Field[/]");
						table.AddColumn("[blue]Value[/]");
						lock (consoleLock)
							ctx.Refresh();

						while (true)
						{
							table.Rows.Clear();
							long reqs = Interlocked.Read(ref totalRequests);
							table.AddRow("[green]Time[/]", DateTime.Now.ToString());
							table.AddRow("[green]Active Connections[/]", Interlocked.Read(ref activeConnections).ToString());
							table.AddRow("[green]Request Rate[/]", requestRate.GetRate(reqs, 0));
							table.AddRow("[green]Total Requests[/]", reqs.ToString());
							table.AddRow("[red]Total Errors[/]", Interlocked.Read(ref totalErrors).ToString());
							table.AddRow("[yellow]Request Time[/]", Interlocked.Read(ref lastRequestTimeMs) + "ms");
							lock (consoleLock)
								ctx.Refresh();
							Thread.Sleep(250);
						}
					}
					catch (Exception ex)
					{
						AnsiConsole.WriteException(ex);
					}
				});
		}

		private static void StartLoadThread(int threadIndex)
		{
			Thread thr = new Thread(LoadThread);
			thr.Name = "Load Thread " + threadIndex;
			thr.IsBackground = true;
			thr.Start(new { threadIndex });
		}
		private static async void LoadThread(object argument)
		{
			dynamic args = argument;
			HttpClient client = null;
			while (true)
			{
				try
				{
					int threadId = args.threadIndex;
					string url = urls[threadId % urls.Length];
					HttpClientHandler handler = new HttpClientHandler();
					handler.MaxConnectionsPerServer = 1;
					client = new HttpClient(handler);
					Interlocked.Increment(ref activeConnections);

					while (!abort)
					{
						Stopwatch sw = Stopwatch.StartNew();
						HttpResponseMessage response = await client.GetAsync(url); // +"?"+StringUtil.GetRandomAlphaNumericString(8)
						string responseContent = await response.Content.ReadAsStringAsync();
						lastRequestTimeMs = sw.ElapsedMilliseconds;
						Interlocked.Increment(ref totalRequests);
					}
				}
				catch (Exception ex)
				{
					Interlocked.Increment(ref totalErrors);
					lock (consoleLock)
					{
						if (!BPUtil.SimpleHttp.HttpProcessor.IsOrdinaryDisconnectException(ex))
							AnsiConsole.WriteLine(ex.FlattenMessages().EscapeMarkup());
					}
				}
				finally
				{
					Interlocked.Decrement(ref activeConnections);
					client.Dispose();
				}
			}
		}
	}
}
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
		const int NUM_CONNECTIONS = 125;
		//static string[] urls = new string[] { "http://localhost:80/" };
		//static string[] urls = new string[] { "http://localhost:80/src/themes.scss" };
		static string[] urls = new string[] { "http://localhost:80/", "http://localhost:80/src/themes.scss", "http://localhost:8080/Log/Test" };
		static void Main(string[] args)
		{
			Stopwatch sw = Stopwatch.StartNew();


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
						ctx.Refresh();

						while (true)
						{
							table.Rows.Clear();
							table.AddRow("[green]Time[/]", DateTime.Now.ToString());
							table.AddRow("[green]Active Connections[/]", Interlocked.Read(ref activeConnections).ToString());
							table.AddRow("[green]Total Requests[/]", Interlocked.Read(ref totalRequests).ToString());
							table.AddRow("[red]Total Errors[/]", Interlocked.Read(ref totalErrors).ToString());
							table.AddRow("[yellow]Request Time[/]", Interlocked.Read(ref lastRequestTimeMs) + "ms");
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
					try
					{
						Stopwatch sw = Stopwatch.StartNew();
						HttpResponseMessage response = await client.GetAsync(url); // +"?"+StringUtil.GetRandomAlphaNumericString(8)
						string responseContent = await response.Content.ReadAsStringAsync();
						lastRequestTimeMs = sw.ElapsedMilliseconds;
						Interlocked.Increment(ref totalRequests);
					}
					catch (Exception ex)
					{
						Interlocked.Increment(ref totalErrors);
						AnsiConsole.WriteLine(ex.FlattenMessages().EscapeMarkup());
					}
					finally
					{
					}
				}
			}
			catch (Exception ex)
			{
				Interlocked.Increment(ref totalErrors);
				AnsiConsole.WriteException(ex);
				AnsiConsole.WriteLine("Thread " + args.threadIndex + " is exiting.".EscapeMarkup());
			}
			finally
			{
				Interlocked.Decrement(ref activeConnections);
				client.Dispose();
			}
		}
	}
}
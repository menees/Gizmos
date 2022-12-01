namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Data;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows;
	using Menees.Windows.Presentation;

	#endregion

	internal partial class App : Application
	{
		#region Constructors

		public App()
		{
			WindowsUtility.InitializeApplication("GizmoDock", null);
		}

		#endregion

		#region Protected Methods

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// Create the IDock window up front because we need it to initialize a Gizmo.
			// However, we may not show this window
			MainWindow dock = new();

			CommandLineArgs args = new(e.Args, dock);
			if (args.CloseAll)
			{
				CloseOtherGizmoDocks();
				this.Shutdown();
			}
			else
			{
				dock.CommandLineArgs = args;
				dock.Show();
			}
		}

		protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
		{
			base.OnSessionEnding(e);

			// The Window.Closing event may not fire during logoff or shutdown,
			// so we need to poke it manually.
			if (this.MainWindow is MainWindow window)
			{
				window.BeginClosing(e);
			}
		}

		#endregion

		#region Private Methods

		private static void CloseOtherGizmoDocks()
		{
			// Send a nice request for each GizmoDock process to close.
			Remote.CloseAll();

			// Wait a bit for them to gracefully exit.
			Thread.Sleep(TimeSpan.FromSeconds(2));

			// Force close all remaining GizmoDock processes except this one.
			Process[] processes = Process.GetProcessesByName("GizmoDock");
			foreach (Process process in processes.Where(p => p.Id != ApplicationInfo.ProcessId))
			{
				// CloseMainWindow will return false unless the GizmoDock process is using ShowInTaskbar = true.
				if (!process.CloseMainWindow())
				{
					process.Kill();
				}
			}
		}

		#endregion
	}
}

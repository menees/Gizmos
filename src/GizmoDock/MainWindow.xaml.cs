namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Navigation;
	using System.Windows.Shapes;
	using System.Windows.Threading;
	using Menees.Windows.Presentation;

	#endregion

	[SuppressMessage(
		"Microsoft.Performance",
		"CA1812:AvoidUninstantiatedInternalClasses",
		Justification = "Created via reflection by WPF.")]
	internal partial class MainWindow : IDock, IGizmoServer
	{
		#region Private Data Members

		private readonly WindowSaver saver;
		private IDisposable? server;

		#endregion

		#region Constructors

		public MainWindow()
		{
			this.InitializeComponent();
			this.saver = new WindowSaver(this)
			{
				AutoLoad = false,
				AutoSave = false,
			};
			this.saver.LoadSettings += this.Saver_LoadSettings;
			this.saver.SaveSettings += this.Saver_SaveSettings;
		}

		#endregion

		#region Public Properties

		public CommandLineArgs? CommandLineArgs { get; internal set; }

		#endregion

		#region Private Properties

		// This has to be explicitly implemented because the property name is the same as this class's name.
		Window IDock.MainWindow => this;

		private Gizmo? CurrentGizmo { get; set; }

		#endregion

		#region IGizmoServer Methods

		void IGizmoServer.Close()
		{
			// Post a message and delay it a little bit so the current (background) thread
			// has time to finish and return to the caller.
			this.Dispatcher.BeginInvoke(
				() =>
				{
					Thread.Sleep(100);
					this.Close();
				},
				DispatcherPriority.ApplicationIdle);
		}

		(double Left, double Top, double Width, double Height) IGizmoServer.GetScreenRectangle()
		{
			double left, top, width, height;
			left = top = width = height = double.NaN;

			// This RPC method will be invoked from a background thread, so we'll use Dispatcher.Invoke
			// to request the window bounds on the main UI thread. There's a potential for deadlock here
			// if the thread pool is full, and the main thread is waiting on a background thread already.
			// Maybe the timeout will get around that rare case (I hope).
			const int MaxWaitSeconds = 2;
			this.Dispatcher.Invoke(
				() =>
				{
					left = this.Left;
					top = this.Top;
					width = this.ActualWidth;
					height = this.ActualHeight;
				},
				TimeSpan.FromSeconds(MaxWaitSeconds));

			return (left, top, width, height);
		}

		void IGizmoServer.MoveTo(double left, double top)
		{
			this.Dispatcher.BeginInvoke(
				() =>
				{
					this.Top = top;
					this.Left = left;
				},
				DispatcherPriority.ApplicationIdle);
		}

		void IGizmoServer.BringToFront()
		{
			this.Dispatcher.BeginInvoke(() => WindowsUtility.BringToFront(this), DispatcherPriority.ApplicationIdle);
		}

		#endregion

		#region Internal Methods

		internal void BeginClosing(CancelEventArgs e)
		{
			// If we don't have a gizmo, then we don't need to save window placement either.
			if (!e.Cancel && this.CurrentGizmo != null && !this.CurrentGizmo.Info.IsTemporary)
			{
				using WindowSaverLock saverLock = new();

				// During OS shutdown or logoff we shouldn't wait long to save settings. The OS will
				// kill the process if we take too long, and that can lead to corrupt settings files.
				using IDisposable? exclusiveLock = saverLock.TryLock(TimeSpan.FromSeconds(2));
				if (exclusiveLock != null)
				{
					this.saver.Save();
				}
			}
		}

		#endregion

		#region Private Methods

		private string GetGizmoSettingsNodePath(bool appendWindowSettingsNodeName)
		{
			StringBuilder sb = new();

			if (this.CurrentGizmo != null)
			{
				// We have to use a '\' separator because that's what the registry and file settings stores support.
				const char Separator = '\\';

				GizmoInfo info = this.CurrentGizmo.Info;
				sb.Append(info.GizmoName);

				if (!info.IsSingleInstance)
				{
					sb.Append(Separator);
					sb.Append(this.CurrentGizmo.InstanceName);
				}

				if (appendWindowSettingsNodeName)
				{
					sb.Append(Separator);
					sb.Append("Window Placement");
				}
			}

			return sb.ToString();
		}

		#endregion

		#region Private Event Handlers

		private void Close_Click(object? sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void Options_Click(object? sender, RoutedEventArgs e)
		{
			if (this.CurrentGizmo != null && this.CurrentGizmo.Info.HasOptions)
			{
				OptionsPage? page = this.CurrentGizmo.CreateOptionsPage();
				if (page != null)
				{
					OptionsDialog dialog = new();
					dialog.Title = this.CurrentGizmo.Info.GizmoName + ' ' + dialog.Title;
					dialog.ShowDialog(this, page);
				}
			}
		}

		private void Grip_Click(object? sender, MouseButtonEventArgs e)
		{
			this.DragMove();
		}

		private void Saver_LoadSettings(object? sender, SettingsEventArgs e)
		{
			if (this.CurrentGizmo != null)
			{
				string path = this.GetGizmoSettingsNodePath(false);
				ISettingsNode gizmoNode = e.SettingsNode.GetSubNode(path);
				this.CurrentGizmo.LoadSettings(gizmoNode);
			}
		}

		private void Saver_SaveSettings(object? sender, SettingsEventArgs e)
		{
			if (this.CurrentGizmo != null)
			{
				string path = this.GetGizmoSettingsNodePath(false);
				ISettingsNode gizmoNode = e.SettingsNode.GetSubNode(path);
				this.CurrentGizmo.SaveSettings(gizmoNode);
			}
		}

		private void Window_Loaded(object? sender, RoutedEventArgs e)
		{
			Gizmo? gizmo = null;
			bool showInTaskbar = false;
			if (this.CommandLineArgs != null)
			{
				gizmo = this.CommandLineArgs.Gizmo;
				showInTaskbar = this.CommandLineArgs.ShowInTaskbar;
			}

			if (gizmo == null || gizmo.Info.IsTemporary)
			{
				showInTaskbar = true;
				this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				this.optionsButton.Visibility = Visibility.Collapsed;
			}

			this.ShowInTaskbar = showInTaskbar;
			if (gizmo != null)
			{
				this.gizmoDock.Children.Clear();
				this.gizmoDock.Children.Add(gizmo);

				this.optionsButton.Visibility = gizmo.Info.HasOptions ? Visibility.Visible : Visibility.Collapsed;
				this.CurrentGizmo = gizmo;

				if (!gizmo.Info.IsTemporary)
				{
					this.Title = gizmo.Info.GizmoName;
					this.saver.SettingsNodeName = this.GetGizmoSettingsNodePath(true);

					// We need to get an exclusive lock before trying to read the settings. All GizmoDock instances
					// share the same .stgx file, and FileSettingsStore requests write access to the file even during
					// Load to ensure it's loading the most preferred settings file.
					using WindowSaverLock saverLock = new();
					using IDisposable? exclusiveLock = saverLock.TryLock(TimeSpan.FromSeconds(5));
					if (exclusiveLock != null)
					{
						this.saver.Load();
					}

					this.server = Remote.CreateServer<IGizmoServer>(gizmo, this);
				}

				// Force a GC after a second so at idle we'll use the least amount of memory.
				Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(task => GC.Collect(), TaskScheduler.Default);
			}
		}

		private void Window_Closing(object? sender, CancelEventArgs e)
		{
			this.BeginClosing(e);
		}

		private void Window_Closed(object? sender, EventArgs e)
		{
			this.server?.Dispose();

			// Some Gizmos may need to explicitly clean up resources.  For example, CPU Stats needs to dispose
			// of its PerformanceCounters, and WorkBreak needs to dispose its NotifyIcon.
			if (this.CurrentGizmo is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		#endregion
	}
}

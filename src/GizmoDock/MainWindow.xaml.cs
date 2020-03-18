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
	using Menees.Windows.Presentation;

	#endregion

	[SuppressMessage(
		"Microsoft.Performance",
		"CA1812:AvoidUninstantiatedInternalClasses",
		Justification = "Created via reflection by WPF.")]
	internal partial class MainWindow : IDock
	{
		#region Private Data Members

		private readonly WindowSaver saver;

		#endregion

		#region Constructors

		public MainWindow()
		{
			this.InitializeComponent();
			this.saver = new WindowSaver(this);
			this.saver.AutoLoad = false;
			this.saver.AutoSave = false;
			this.saver.LoadSettings += this.Saver_LoadSettings;
			this.saver.SaveSettings += this.Saver_SaveSettings;
		}

		#endregion

		#region Public Properties

		public CommandLineArgs CommandLineArgs { get; internal set; }

		#endregion

		#region Private Properties

		// This has to be explicitly implemented because the property name is the same as this class's name.
		Window IDock.MainWindow => this;

		private Gizmo CurrentGizmo { get; set; }

		#endregion

		#region Internal Methods

		internal void BeginClosing(CancelEventArgs e)
		{
			// If we don't have a gizmo, then we don't need to save window placement either.
			if (!e.Cancel && this.CurrentGizmo != null && !this.CurrentGizmo.Info.IsTemporary)
			{
				// Because all gizmos share the same .stgx file, we'll use the full path to the .exe as the mutex name
				// (since the .stgx file will typically be in the same folder as the .exe).  However the '\' character is
				// reserved for kernel object namespaces, so we have to replace it.
				// http://stackoverflow.com/questions/4313756/creating-a-mutex-throws-a-directorynotfoundexception.
				using (Mutex mutex = new Mutex(false, ApplicationInfo.ExecutableFile.Replace('\\', '_')))
				{
					if (mutex.WaitOne())
					{
						try
						{
							this.saver.Save();
						}
						finally
						{
							mutex.ReleaseMutex();
						}
					}
				}
			}
		}

		#endregion

		#region Private Methods

		private string GetGizmoSettingsNodePath(bool appendWindowSettingsNodeName)
		{
			StringBuilder sb = new StringBuilder();

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

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void Options_Click(object sender, RoutedEventArgs e)
		{
			if (this.CurrentGizmo != null && this.CurrentGizmo.Info.HasOptions)
			{
				OptionsPage page = this.CurrentGizmo.CreateOptionsPage();
				if (page != null)
				{
					OptionsDialog dialog = new OptionsDialog();
					dialog.Title = this.CurrentGizmo.Info.GizmoName + ' ' + dialog.Title;
					dialog.ShowDialog(this, page);
				}
			}
		}

		private void Grip_Click(object sender, MouseButtonEventArgs e)
		{
			this.DragMove();
		}

		private void Saver_LoadSettings(object sender, SettingsEventArgs e)
		{
			if (this.CurrentGizmo != null)
			{
				string path = this.GetGizmoSettingsNodePath(false);
				ISettingsNode gizmoNode = e.SettingsNode.GetSubNode(path, true);
				this.CurrentGizmo.LoadSettings(gizmoNode);
			}
		}

		private void Saver_SaveSettings(object sender, SettingsEventArgs e)
		{
			if (this.CurrentGizmo != null)
			{
				string path = this.GetGizmoSettingsNodePath(false);
				ISettingsNode gizmoNode = e.SettingsNode.GetSubNode(path, true);
				this.CurrentGizmo.SaveSettings(gizmoNode);
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Gizmo gizmo = null;
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
					this.saver.Load();
				}

				// Force a GC after a second so at idle we'll use the least amount of memory.
				Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(task => GC.Collect(), TaskScheduler.Default);
			}
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			this.BeginClosing(e);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
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

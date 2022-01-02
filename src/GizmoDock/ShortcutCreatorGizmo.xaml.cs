namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
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
	using Menees.Windows;
	using Menees.Windows.Presentation;
	using Microsoft.Win32;

	#endregion

	[GizmoInfo(GizmoName = "Gizmo Shortcut Creator", IsSingleInstance = true)]
	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created via reflection.")]
	internal partial class ShortcutCreatorGizmo
	{
		#region Constructors

		public ShortcutCreatorGizmo()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Protected Methods

		protected override void OnLoadSettings(ISettingsNode settings)
		{
			base.OnLoadSettings(settings);

			// Initialize the data context with "no gizmos", so things will be disabled if an assembly isn't found.
			this.SetDataContext(NoGizmosFound.GizmoTypes, false);

			string defaultAssembly = System.IO.Path.Combine(ApplicationInfo.BaseDirectory, "Menees.Gizmos.Standard.dll");
			string assemblyName = settings.GetValue("AssemblyName", defaultAssembly);
			if (!string.IsNullOrEmpty(assemblyName) && File.Exists(assemblyName))
			{
				if (this.TryGetGizmoTypes(assemblyName, false))
				{
					this.assembly.Text = assemblyName;
				}
			}

			this.GenerateInstanceName(false);
		}

		protected override void OnSaveSettings(ISettingsNode settings)
		{
			base.OnSaveSettings(settings);
			settings.SetValue("AssemblyName", this.assembly.Text);
		}

		#endregion

		#region Private Methods

		private void GenerateInstanceName(bool keepIfCustom)
		{
			const string Prefix = "Instance_";
			const string DateTimeFormat = "yyyyMMdd_HHmmss";

			string instanceName = this.instance.Text.Trim();
			if (!keepIfCustom ||
				string.IsNullOrEmpty(instanceName) ||
				(instanceName.StartsWith(Prefix) &&
				DateTime.TryParseExact(
					instanceName.Substring(Prefix.Length),
					DateTimeFormat,
					CultureInfo.CurrentCulture,
					DateTimeStyles.None,
					out _)))
			{
				// Generate a default instance name that's less ugly than a GUID but still unique enough.
#pragma warning disable MEN013 // Use UTC time. A local time is fine here.
				this.instance.Text = Prefix + DateTime.Now.ToString(DateTimeFormat);
#pragma warning restore MEN013 // Use UTC time
			}
		}

		private bool TryGetGizmoTypes(string assemblyName, bool showErrors)
		{
			List<string> errors = new();

			if (!File.Exists(assemblyName))
			{
				errors.Add("The specified assembly does not exist: " + assemblyName);
			}
			else
			{
				using (new WaitCursor())
				{
					IList<GizmoInfo>? gizmoTypes = GetGizmoTypes(assemblyName, errors);
					bool foundTypes = gizmoTypes != null && gizmoTypes.Count > 0;

					// Use a "dummy" list if no types are found, so that other data-bound controls will enable/disable correctly.
					if (!foundTypes)
					{
						gizmoTypes = NoGizmosFound.GizmoTypes;
					}

					this.SetDataContext(gizmoTypes!, foundTypes);
				}
			}

			bool result = errors.Count == 0;
			if (!result && showErrors)
			{
				WindowsUtility.ShowError(this, string.Join(Environment.NewLine, errors));
			}

			return result;
		}

		private void SetDataContext(IList<GizmoInfo> gizmoTypes, bool foundTypes)
		{
			this.DataContext = gizmoTypes;
			this.gizmos.SelectedItem = gizmoTypes[0];
			this.gizmos.IsEnabled = foundTypes;
		}

		private bool TryGetShortcutInfo([MaybeNullWhen(false)] out ShortcutInfo info)
		{
			string? errorMessage = null;
			Control? focusControl = null;

			GizmoInfo? gizmo = this.gizmos.SelectedItem as GizmoInfo;
			string? instanceName = null;
			if (gizmo == null)
			{
				errorMessage = "You must select a gizmo.";
				focusControl = this.gizmos;
			}
			else
			{
				instanceName = this.instance.Text.Trim();
				if (!gizmo.IsSingleInstance && string.IsNullOrEmpty(instanceName))
				{
					errorMessage = "You must specify an instance name.";
					focusControl = this.instance;
				}
			}

			bool result = errorMessage.IsEmpty();
			if (result && gizmo != null)
			{
				info = new ShortcutInfo(gizmo, gizmo.IsSingleInstance ? null : instanceName, this.showInTaskbar.IsChecked ?? false);
			}
			else
			{
				info = null;
				WindowsUtility.ShowError(this, errorMessage!);
			}

			if (focusControl != null)
			{
				focusControl.Focus();
			}

			return result;
		}

		#endregion

		#region Private Event Handlers

		private void CloseClick(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void SelectAssemblyClick(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new()
			{
				DefaultExt = ".dll",
				Filter = "DLLs (*.dll)|*.dll|All Files (*.*)|*.*",
				FileName = this.assembly.Text,
				Title = "Select Custom Gizmo Library",
			};

			if (this.Dock != null && (dialog.ShowDialog(this.Dock.MainWindow) ?? false))
			{
				using (new WaitCursor())
				{
					string assemblyName = dialog.FileName;
					if (this.TryGetGizmoTypes(assemblyName, true))
					{
						this.assembly.Text = assemblyName;
					}
				}
			}
		}

		private void RunClick(object sender, RoutedEventArgs e)
		{
			if (this.TryGetShortcutInfo(out ShortcutInfo? info))
			{
				try
				{
					using (new WaitCursor())
					{
						string commandLine = info.BuildCommandLine(false);
						Process.Start(ApplicationInfo.ExecutableFile, commandLine);
					}
				}
				catch (Win32Exception ex)
				{
					WindowsUtility.ShowError(this, ex.Message);
				}
			}
		}

		private void CopyClick(object sender, RoutedEventArgs e)
		{
			if (this.TryGetShortcutInfo(out ShortcutInfo? info))
			{
				using (new WaitCursor())
				{
					string commandLine = info.BuildCommandLine(true);
					Clipboard.SetText(commandLine);
				}
			}
		}

		private void CreateShortcutClick(object sender, RoutedEventArgs e)
		{
			if (this.Dock != null && this.TryGetShortcutInfo(out ShortcutInfo? info))
			{
				SaveFileDialog dialog = new()
				{
					DefaultExt = ".lnk",
					Filter = "Shortcuts (*.lnk)|*.lnk|All Files (*.*)|*.*",
					FileName = info.Gizmo.GizmoName + ".lnk",
					Title = "Create Shortcut",
				};

				if (dialog.ShowDialog(this.Dock.MainWindow) ?? false)
				{
					using (new WaitCursor())
					{
						info.CreateShortcut(dialog.FileName);
					}
				}
			}
		}

		private void GenerateInstanceNameClick(object sender, RoutedEventArgs e)
		{
			this.GenerateInstanceName(false);
		}

		private void TitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			Window root = Window.GetWindow(this);
			if (root != null)
			{
				root.DragMove();
			}
		}

		#endregion

		#region Private Types

		private sealed class ShortcutInfo
		{
			#region Constructors

			public ShortcutInfo(GizmoInfo gizmo, string? instanceName, bool showInTaskbar)
			{
				this.Gizmo = gizmo;
				this.InstanceName = instanceName;
				this.ShowInTaskbar = showInTaskbar;
			}

			#endregion

			#region Public Properties

			public GizmoInfo Gizmo { get; }

			public string? InstanceName { get; }

			public bool ShowInTaskbar { get; }

			#endregion

			#region Public Methods

			public string BuildCommandLine(bool includeExecutable)
			{
				StringBuilder result = new();

				if (includeExecutable)
				{
					result.Append('"').Append(ApplicationInfo.ExecutableFile).Append("\" ");
				}

				Type type = this.Gizmo.GizmoType;
				result.Append("/Assembly \"").Append(type.Assembly.Location).Append("\" ");
				result.Append("/Type ").Append(type.FullName);

				if (!string.IsNullOrEmpty(this.InstanceName))
				{
					result.Append(" /Instance ").Append(this.InstanceName);
				}

				if (this.ShowInTaskbar)
				{
					result.Append(" /ShowInTaskbar");
				}

				return result.ToString();
			}

			public void CreateShortcut(string shortcutFileName)
			{
				// From: http://stackoverflow.com/questions/234231/creating-application-shortcut-in-a-directory
				// WshShortcut Object Properties and Methods: http://msdn.microsoft.com/en-us/library/f5y78918.aspx
				dynamic? shell = ComUtility.CreateInstance("WScript.Shell");
				if (shell != null)
				{
					try
					{
						var link = shell.CreateShortcut(shortcutFileName);
						try
						{
							link.TargetPath = ApplicationInfo.ExecutableFile;
							link.Arguments = this.BuildCommandLine(false);
							link.Description = "Opens the \"" + this.Gizmo.GizmoName + "\" gizmo.";
							link.Save();
						}
						finally
						{
							ComUtility.FinalRelease(link);
						}
					}
					finally
					{
						ComUtility.FinalRelease(shell);
					}
				}
			}

			#endregion
		}

		[GizmoInfo(GizmoName = "No gizmos were found.", IsSingleInstance = true)]
		[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created via reflection.")]
		private sealed class NoGizmosFound : Gizmo
		{
			#region Public Fields

			public static readonly IList<GizmoInfo> GizmoTypes = new[] { new GizmoInfo(typeof(NoGizmosFound)) };

			#endregion
		}

		#endregion
	}
}

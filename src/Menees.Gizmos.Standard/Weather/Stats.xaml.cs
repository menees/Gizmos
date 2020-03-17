[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Microsoft.Design",
	"CA1020:AvoidNamespacesWithFewTypes",
	Scope = "namespace",
	Target = "Menees.Gizmos.Weather",
	Justification = "I want each gizmo in its own namespace.")]

namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
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
	using System.Windows.Threading;
	using Menees.Windows.Presentation;

	#endregion

	[GizmoInfo(GizmoName = nameof(Weather), IsSingleInstance = false)]
	public partial class Stats
	{
		#region Private Data Members

		private DispatcherTimer timer;
		private Settings settings;
		private WeatherInfo weather;
		private bool showingError;

		#endregion

		#region Constructors

		public Stats()
		{
			this.InitializeComponent();

			this.settings = Settings.Default;
			this.weather = new WeatherInfo(this.settings);

			this.timer = new DispatcherTimer();
			this.timer.Interval = Properties.Settings.Default.WeatherRefreshInterval;
			this.timer.Tick += (s, e) => this.UpdateDisplay();
		}

		#endregion

		#region Internal Properties

		internal Settings Settings
		{
			get
			{
				return this.settings;
			}

			set
			{
				this.settings = value ?? Settings.Default;
				this.UpdateDisplay();
			}
		}

		#endregion

		#region Protected Methods

		protected override void OnLoadSettings(ISettingsNode settings)
		{
			base.OnLoadSettings(settings);

			// Create a new, modifiable instance rather than using the default, read-only instance.
			this.settings = new Settings();
			this.settings.Load(settings);

			this.timer.IsEnabled = true;

			this.Dispatcher.InvokeAsync(this.UpdateDisplay, DispatcherPriority.ApplicationIdle);
		}

		protected override void OnSaveSettings(ISettingsNode settings)
		{
			base.OnSaveSettings(settings);
			this.settings.Save(settings);
		}

		protected override OptionsPage OnCreateOptionsPage() => new StatsOptionsPage(this);

		#endregion

		#region Private Methods

		[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Top-level UI method.")]
		private void UpdateDisplay()
		{
			try
			{
				using (new WaitCursor())
				{
					Provider provider = Provider.Create();
					this.weather = provider.GetWeatherAsync(this.settings).Result;

					// Changing the root DataContext will cause all of the bindings to update.
					this.DataContext = this.weather;
					this.UpdateTimer(this.weather.IsValid);
				}
			}
			catch (Exception ex)
			{
				var properties = this.GetLogProperties();
				properties.Add("Location", this.settings.UserLocation);
				Log.Error(typeof(Stats), "An error occurred updating the display.", ex, properties);
				this.UpdateTimer(false);

				// If an error occurs on every timer interval, make sure we only show one MessageBox at a time.
				if (!this.showingError)
				{
					this.showingError = true;
					try
					{
						StringBuilder sb = new StringBuilder();
						Exceptions.ForEach(ex, (exception, depth, parent) => sb.Append('\t', depth).Append(exception.Message).AppendLine());
						if (ApplicationInfo.IsDebugBuild)
						{
							// Show the root exception's type and call stack in debug builds.
							sb.AppendLine().AppendLine(ex.ToString());
						}

						string message = string.Format(
							"{0}: {1}{2}{2}{3}",
							this.Info.GizmoName,
							this.settings.UserLocation,
							Environment.NewLine,
							sb.ToString().Trim());
						WindowsUtility.ShowError(this, message);
					}
					finally
					{
						this.showingError = false;
					}
				}
			}
		}

		private void UpdateTimer(bool updatedStats)
		{
			// Use the same short-but-doubling refresh interval logic that the RunningAhead gizmo uses if an error occurs.
			var settings = Properties.Settings.Default;
			RunningAhead.Stats.UpdateTimer(this.timer, settings.WeatherRefreshInterval, settings.HttpRequestTimeout, updatedStats);
		}

		#endregion

		#region Private Event Handlers

		private void Refresh_Click(object sender, RoutedEventArgs e)
		{
			this.UpdateDisplay();
		}

		private void MoreInfo_Click(object sender, RoutedEventArgs e)
		{
			if (this.weather != null && this.weather.MoreInfoLink != null)
			{
				WindowsUtility.ShellExecute(this, this.weather.MoreInfoLink.ToString());
			}
		}

		#endregion
	}
}

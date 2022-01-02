namespace Menees.Gizmos.Cpu
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Security;
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

	[GizmoInfo(GizmoName = "CPU Stats", IsSingleInstance = true)]
	public sealed partial class Stats : IDisposable, INotifyPropertyChanged
	{
		#region Public Fields

		public const int MaxItems = 5;

		#endregion

		#region Private Data Members

		// This should only be set to true when testing to see how the gizmo behaves if the user can't access performance counter information.
		private const bool TestAsIfNoSecurityAccess = false;

		private readonly DispatcherTimer timer;
		private readonly PerformanceCounter? overallCpuCounter;
		private readonly ProcessInfoCache? cache;
		private readonly Tuple<TextBlock, TextBlock>[] lines;

		// We need to throw away the first set of counter measurements.  They're crap because the
		// counters need at least two measurements about a second apart to calculate good values.
		// http://blogs.msdn.com/b/bclteam/archive/2006/06/02/618156.aspx
		private bool initializedCounters;

		#endregion

		#region Constructors

		public Stats()
		{
			this.InitializeComponent();

			this.lines = new Tuple<TextBlock, TextBlock>[MaxItems];
			for (int i = 1; i <= MaxItems; i++)
			{
				TextBlock name = (TextBlock)this.FindName("procName" + i);
				TextBlock usage = (TextBlock)this.FindName("procUsage" + i);
				this.lines[i - 1] = new Tuple<TextBlock, TextBlock>(name, usage);
			}

			const int ItemLimit = 3;
			this.TopCount = ItemLimit;
			this.ShowTenths = true;
			this.timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1),
			};
			this.timer.Tick += (s, e) => this.RefreshData();

			// Read a counter value up front to get it initialized and to see if we have access.
			this.overallCpuCounter = ProcessInfoCache.TryGetCounterInstance("Processor", "% Processor Time", "_Total", out Exception? ex);
			if (this.overallCpuCounter != null)
			{
				this.overallCpuCounter.NextValue();
				this.cache = new ProcessInfoCache();
			}
			else
			{
				this.cpuBorder.Background = Brushes.Red;
				string message = ex != null ? ex.Message : "Unable to read the CPU performance counter data.";
				if (ex is SecurityException || ex is UnauthorizedAccessException)
				{
					message += Environment.NewLine + "The user must be a member of the Administrators group or the Performance Monitor Users group.";
				}

				// Put this in the message pump as a low priority message to be shown after everything else is processed.
				this.Dispatcher.InvokeAsync(() => WindowsUtility.ShowError(this, message), DispatcherPriority.ApplicationIdle);
			}

			// Call this now to initialize all the counters and the display lines.
			this.RefreshData();
		}

		#endregion

		#region Public Events

		public event PropertyChangedEventHandler? PropertyChanged;

		#endregion

		#region Public Properties

		public bool IsPaused
		{
			get
			{
				return !this.timer.IsEnabled;
			}

			set
			{
				if (this.IsPaused != value)
				{
					this.timer.IsEnabled = !value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsPaused)));
				}
			}
		}

		#endregion

		#region Internal Properties

		internal TimeSpan RefreshInterval
		{
			get
			{
				return this.timer.Interval;
			}

			set
			{
				this.timer.Interval = value;
			}
		}

		internal bool ShowTenths { get; set; }

		internal int TopCount { get; set; }

		internal bool ShowZeros { get; set; }

		#endregion

		#region Public Methods

		public void RefreshData()
		{
			bool clearValues = true;
			if (this.overallCpuCounter != null)
			{
				float usage = this.overallCpuCounter.NextValue();

				if (this.initializedCounters)
				{
					clearValues = false;
					this.overallBar.IsIndeterminate = false;
					this.overallBar.Value = usage;
					this.overallUsage.Text = this.FormatPercent(usage);
				}
			}

			if (clearValues)
			{
				this.overallBar.IsIndeterminate = true;
				this.overallUsage.Text = "?";
			}

			clearValues = true;
			if (this.cache != null)
			{
				// Optionally, leave out processes that will round down to zero; they must round up to at least 0.1% or 1%.
				const float ShowTenthsLimit = 0.05f;
				const float ShowWholeLimit = 0.5f;
				float minValue = this.ShowZeros ? 0 : this.ShowTenths ? ShowTenthsLimit : ShowWholeLimit;
				Dictionary<string, float> usage = this.cache.Refresh(minValue);
				var topItems = usage
					.Where(p => !float.IsNaN(p.Value) && p.Key != "_Total" && p.Key != "Idle")
					.OrderByDescending(p => p.Value).ThenBy(p => p.Key)
					.Take(this.TopCount)
					.Select(p => new Tuple<string, float>(TrimInstanceNumber(p.Key), p.Value))
					.ToArray();

				if (this.initializedCounters)
				{
					clearValues = false;
					this.UpdateLines(topItems);
				}
			}

			if (clearValues)
			{
				this.UpdateLines(null);
			}

			this.initializedCounters = true;
		}

		public void Dispose()
		{
			if (this.overallCpuCounter != null)
			{
				this.overallCpuCounter.Dispose();
			}
		}

		#endregion

		#region Protected Methods

		protected override void OnLoadSettings(ISettingsNode settings)
		{
			base.OnLoadSettings(settings);

			this.ShowZeros = settings.GetValue("Show Zeros", this.ShowZeros);
			this.ShowTenths = settings.GetValue("Show Tenths", this.ShowTenths);
			this.TopCount = Math.Min(MaxItems, Math.Max(0, settings.GetValue("Top Count", this.TopCount)));
			double seconds = settings.GetValue("Refresh Seconds", this.timer.Interval.TotalSeconds);
			if (seconds >= 1)
			{
				this.timer.Interval = TimeSpan.FromSeconds(seconds);
			}

			// Note: We're not saving and loading whether we're in the Paused state because
			// on startup we always need to refresh twice (the second time on a timer) to
			// avoid leaving the display in the "Indeterminate" state.
			this.IsPaused = false;
		}

		protected override void OnSaveSettings(ISettingsNode settings)
		{
			base.OnSaveSettings(settings);

			settings.SetValue("Show Zeros", this.ShowZeros);
			settings.SetValue("Show Tenths", this.ShowTenths);
			settings.SetValue("Top Count", this.TopCount);
			settings.SetValue("Refresh Seconds", this.timer.Interval.TotalSeconds);
		}

		protected override OptionsPage OnCreateOptionsPage() => new StatsOptionsPage(this);

		#endregion

		#region Private Methods

		private static string TrimInstanceNumber(string name)
		{
			string result = name;

			if (!string.IsNullOrEmpty(name))
			{
				// If the name ends with #N..N, then we want to strip off the #N..N part.
				int poundIndex = name.IndexOf('#');
				if (poundIndex > 0 && poundIndex + 1 < name.Length)
				{
					bool allDigits = true;
					for (int i = poundIndex + 1; i < name.Length; i++)
					{
						if (!char.IsDigit(name[i]))
						{
							allDigits = false;
							break;
						}
					}

					if (allDigits)
					{
						result = name.Substring(0, poundIndex);
					}
				}
			}

			return result;
		}

		private string FormatPercent(float value)
		{
			// .Net's P format multiplies by 100, so we'll do this manually.
			string format = this.ShowTenths ? "{0:F1} %" : "{0:F0} %";
			return string.Format(format, value);
		}

		private void UpdateLines(Tuple<string, float>[]? items)
		{
			// Note: It possible to have fewer items than lines, but it should be rare.
			for (int i = 0; i < this.lines.Length; i++)
			{
				var line = this.lines[i];

				if (items != null && i < items.Length)
				{
					var item = items[i];

					line.Item1.Text = item.Item1;
					line.Item2.Text = this.FormatPercent(item.Item2);
				}
				else
				{
					// We want to show at least TopCount rows, but we want rows beyond that to collapse.
					string dummyValue = i < this.TopCount ? " " : string.Empty;
					line.Item1.Text = dummyValue;
					line.Item2.Text = dummyValue;
				}
			}
		}

		#endregion

		#region Private Event Handlers

		private void ContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			bool isPaused = this.IsPaused;
			this.oneSecondMenu.IsChecked = !isPaused && this.timer.Interval == (TimeSpan)this.oneSecondMenu.Tag;
			this.fiveSecondsMenu.IsChecked = !isPaused && this.timer.Interval == (TimeSpan)this.fiveSecondsMenu.Tag;
			this.pausedMenu.IsChecked = this.IsPaused;
			e.Handled = true;
		}

		private void Refresh_Click(object sender, RoutedEventArgs e)
		{
			this.RefreshData();
			e.Handled = true;
		}

		private void Time_Click(object sender, RoutedEventArgs e)
		{
			this.RefreshData();
			this.timer.Interval = (TimeSpan)((MenuItem)sender).Tag;
			this.IsPaused = false;
			e.Handled = true;
		}

		private void Pause_Click(object sender, RoutedEventArgs e)
		{
			this.IsPaused = !this.IsPaused;
			e.Handled = true;
		}

		#endregion

		#region Private Types

		private sealed class ProcessInfoCache
		{
			#region Private Data Members

			// The division needs this as a float, so we'll do that once up front.
			private static readonly float ProcessorCount = Environment.ProcessorCount;

			private readonly PerformanceCounterCategory category;
			private Dictionary<string, CounterSample>? previousSamples;

			#endregion

			#region Constructors

			public ProcessInfoCache()
			{
				this.category = new PerformanceCounterCategory("Process");
			}

			#endregion

			#region Public Methods

			public static PerformanceCounter? TryGetCounterInstance(string category, string counter, string instance, out Exception? exception)
			{
				PerformanceCounter? result = TryGetPerformanceData(() => new PerformanceCounter(category, counter, instance, true), out exception);
				return result;
			}

			public Dictionary<string, float> Refresh(float minValue)
			{
				InstanceDataCollectionCollection? allData = TryGetPerformanceData(() => this.category.ReadCategory(), out Exception? ex);

				Dictionary<string, float> result = new(StringComparer.CurrentCultureIgnoreCase);
				if (allData != null)
				{
					InstanceDataCollection processData = allData["% Processor Time"];
					if (processData != null)
					{
						Dictionary<string, CounterSample> currentSamples = new();
						foreach (InstanceData data in processData.Values)
						{
							string instanceName = data.InstanceName;
							CounterSample currentSample = data.Sample;
							currentSamples[instanceName] = currentSample;

							if (this.previousSamples != null && this.previousSamples.TryGetValue(instanceName, out CounterSample previousSample))
							{
								// We have to average the usage across all the logical processors.
								// On an 8 processor machine, the counter can return up to 800!
								float value = CounterSampleCalculator.ComputeCounterValue(previousSample, currentSample) / ProcessorCount;
								result[data.InstanceName] = value >= minValue ? value : float.NaN;
							}
						}

						this.previousSamples = currentSamples;
					}
				}

				return result;
			}

			#endregion

			#region Private Methods

			private static T? TryGetPerformanceData<T>(Func<T> getPerformanceData, out Exception? exception)
				where T : class
			{
				exception = null;
				T? result = null;

				try
				{
					if (ApplicationInfo.IsDebugBuild && TestAsIfNoSecurityAccess)
					{
						throw new SecurityException("Testing as if the user has no security access to performance data.");
					}

					result = getPerformanceData();
				}
				catch (SecurityException ex)
				{
					// The user may not have permissions to read counters (e.g., not in the Performance Monitor Users group).
					exception = ex;
				}
				catch (UnauthorizedAccessException ex)
				{
					// The user may not have permissions to read counters (e.g., not in the Performance Monitor Users group).
					exception = ex;
				}
				catch (Win32Exception ex)
				{
					// The counter instance/category may already have gone away.
					exception = ex;
				}
				catch (InvalidOperationException ex)
				{
					// The counter instance/category may already have gone away.
					exception = ex;
				}

				return result;
			}

			#endregion
		}

		#endregion
	}
}

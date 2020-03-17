[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Microsoft.Design",
	"CA1020:AvoidNamespacesWithFewTypes",
	Scope = "namespace",
	Target = "Menees.Gizmos.RunningAhead",
	Justification = "I want each gizmo in its own namespace.")]

namespace Menees.Gizmos.RunningAhead
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
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
	using System.Xml;
	using System.Xml.Linq;
	using Menees.Windows.Presentation;

	#endregion

	[GizmoInfo(GizmoName = "RunningAhead Stats")]
	public partial class Stats
	{
		#region Private Data Members

		private const string LogsBasePath = "logs";

		private static readonly TimeSpan RequestTimeout = Properties.Settings.Default.HttpRequestTimeout;

		private readonly DispatcherTimer timer;

		#endregion

		#region Constructors

		public Stats()
		{
			this.InitializeComponent();

			if (ApplicationInfo.IsDebugBuild)
			{
				this.LogId = "23fbfe3407d54034bb604d607cb56919"; // BMenees LogId
			}

			this.RefreshInterval = TimeSpan.FromHours(2);
			this.UseDefaultHeader = true;
			this.FooterFormat = FooterFormat.RefreshedDateTime;

			this.ClearDisplay();

			this.timer = new DispatcherTimer();
			this.timer.Interval = this.RefreshInterval;
			this.timer.Tick += (s, e) => this.UpdateDisplay();
		}

		#endregion

		#region Internal Properties

		internal string LogId { get; set; }

		internal TimeSpan RefreshInterval { get; set; }

		internal bool UseDefaultHeader { get; set; }

		internal string Header { get; set; }

		internal StatsFormat StatsFormat { get; set; }

		internal FooterFormat FooterFormat { get; set; }

		#endregion

		#region Internal Methods

		internal static bool ValidateLogId(string logId, out string errorMessage)
		{
			errorMessage = null;

			// Skip the validation if the Shift key is held down.
			bool result = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
			if (!result)
			{
				try
				{
					using (new WaitCursor())
					{
						Uri uri = GetRunningAheadUri(LogsBasePath, logId);

						// We need to prevent redirects so we can tell whether the first request works or not.
						// Otherwise, we'll just get back an OK after redirecting to the RA home page.
						// http://blogs.msdn.com/b/henrikn/archive/2012/08/07/httpclient-httpclienthandler-and-httpwebrequesthandler.aspx
						using (HttpClientHandler handler = new HttpClientHandler { AllowAutoRedirect = false })
						using (HttpClient client = new HttpClient(handler) { Timeout = RequestTimeout })
						{
							Task<HttpResponseMessage> task = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
							task.Wait();

							HttpResponseMessage response = task.Result;

							// Treat an HTTP 302 (Found) code as success.
							if (response.StatusCode != HttpStatusCode.Found)
							{
								response.EnsureSuccessStatusCode();
							}

							result = true;
						}
					}
				}
				catch (AggregateException ex)
				{
					errorMessage = GetAggregateExceptionMessage(ex);
				}
				catch (HttpRequestException ex)
				{
					errorMessage = ex.Message;
				}
			}

			return result;
		}

		internal static void UpdateTimer(DispatcherTimer timer, TimeSpan standardRefreshInterval, TimeSpan minimumRefreshInterval, bool updatedStats)
		{
			if (updatedStats)
			{
				// We need to update the timer interval to the standard value in case:
				// - this is the first call and it needs to be initialized to the persisted value,
				// - it was just changed to a new value in the Options,
				// - it was temporarily set to a shorter value due to an HTTP request failure.
				timer.Interval = standardRefreshInterval;
			}
			else if (timer.Interval == standardRefreshInterval)
			{
				// Retry after a short amount of time.  We'll increase this if errors keep occuring.
				timer.Interval = minimumRefreshInterval;
			}
			else
			{
				// Double the interval as long as we're getting errors but only up to the requested RefreshInterval.
				TimeSpan newInterval = TimeSpan.FromTicks(2 * timer.Interval.Ticks);
				if (newInterval >= standardRefreshInterval)
				{
					// Make a value slightly different from RefreshInterval so the equality check
					// in the previous "if" won't match it.  We don't need to go through the increasing
					// interval sequence again if the site is still returning errors.
					timer.Interval = standardRefreshInterval.Add(TimeSpan.FromSeconds(1));
				}
				else
				{
					timer.Interval = newInterval;
				}
			}
		}

		internal void UpdateDisplay()
		{
			this.ClearDisplay();
			this.UpdateTitle();
			bool updatedStats = this.UpdateStats();
			this.UpdateFooter();
			UpdateTimer(this.timer, this.RefreshInterval, RequestTimeout, updatedStats);
		}

		#endregion

		#region Protected Methods

		protected override void OnLoadSettings(ISettingsNode settings)
		{
			base.OnLoadSettings(settings);

			this.LogId = settings.GetValue(nameof(this.LogId), this.LogId);

			string value = settings.GetValue(nameof(this.RefreshInterval), this.RefreshInterval.ToString());
			if (TimeSpan.TryParse(value, out TimeSpan interval))
			{
				this.RefreshInterval = interval;
			}

			this.UseDefaultHeader = settings.GetValue(nameof(this.UseDefaultHeader), this.UseDefaultHeader);
			this.Header = settings.GetValue(nameof(this.Header), this.Header);
			this.StatsFormat = settings.GetValue(nameof(this.StatsFormat), this.StatsFormat);
			this.FooterFormat = settings.GetValue(nameof(this.FooterFormat), this.FooterFormat);

			this.timer.IsEnabled = true;

			this.Dispatcher.InvokeAsync(this.UpdateDisplay, DispatcherPriority.ApplicationIdle);
		}

		protected override void OnSaveSettings(ISettingsNode settings)
		{
			base.OnSaveSettings(settings);

			settings.SetValue(nameof(this.LogId), this.LogId);
			settings.SetValue(nameof(this.RefreshInterval), this.RefreshInterval.ToString());
			settings.SetValue(nameof(this.UseDefaultHeader), this.UseDefaultHeader);
			settings.SetValue(nameof(this.Header), this.Header);
			settings.SetValue(nameof(this.StatsFormat), this.StatsFormat);
			settings.SetValue(nameof(this.FooterFormat), this.FooterFormat);

			this.timer.IsEnabled = false;
		}

		protected override OptionsPage OnCreateOptionsPage()
		{
			StatsOptionsPage result = new StatsOptionsPage(this);
			return result;
		}

		#endregion

		#region Private Methods

		private static bool GetHttpResponse(Uri uri, out string response)
		{
			response = null;
			bool result = false;

			try
			{
				using (HttpClient client = new HttpClient { Timeout = RequestTimeout })
				{
					Task<string> task = client.GetStringAsync(uri);
					task.Wait();
					response = task.Result;
					result = true;
				}
			}
			catch (AggregateException ex)
			{
				response = GetAggregateExceptionMessage(ex);
			}

			return result;
		}

		private static string GetAggregateExceptionMessage(AggregateException ex)
		{
			string result;

			Exception baseEx = ex.GetBaseException();
			if (baseEx is OperationCanceledException)
			{
				result = "The request timed out.";
			}
			else
			{
				result = baseEx.Message;
			}

			return result;
		}

		private static XElement GetXhtmlData(string response, bool useRARecordsTable)
		{
			XElement result = null;

			string xmlText = null;
			if (useRARecordsTable)
			{
				// Skip past the RANotes <p> since it can contain invalid XML (e.g., an '&' char instead of an &amp; entity).
				const string Prefix = " class=\\\"RARecords\\\">";
				int startIndex = response.IndexOf(Prefix);
				if (startIndex >= 0)
				{
					response = response.Substring(startIndex);

					const string Suffix = "</table>";
					startIndex = response.IndexOf(Suffix);
					if (startIndex >= 0)
					{
						xmlText = "<table" + response.Substring(0, startIndex + Suffix.Length);
					}
				}
			}
			else
			{
				const string Prefix = "div.innerHTML = \"";
				int startIndex = response.IndexOf(Prefix);
				if (startIndex >= 0)
				{
					response = response.Substring(startIndex + Prefix.Length);

					const string Suffix = "<div class=\\\"RAFooter";
					startIndex = response.IndexOf(Suffix);
					if (startIndex >= 0)
					{
						xmlText = response.Substring(0, startIndex);
					}
				}
			}

			if (!string.IsNullOrEmpty(xmlText))
			{
				xmlText = xmlText.Replace("\\\"", "\"");
				xmlText = "<RA>" + xmlText + "</RA>";

				try
				{
					result = XElement.Parse(xmlText);
				}
#pragma warning disable CC0004 // Catch block cannot be empty. It contains a comment explaining why.
				catch (XmlException)
				{
					// If RA changes its HTML format, we require code changes.
				}
#pragma warning restore CC0004 // Catch block cannot be empty
			}

			return result;
		}

		private static Uri GetRunningAheadUri(string section, string logId, string argument = null)
		{
			UriBuilder result = new UriBuilder("http", "www.runningahead.com");

			// If the user clicks View Log with no LogId set, then we should just show the RA home page.
			StringBuilder path = new StringBuilder();
			if (!string.IsNullOrEmpty(logId))
			{
				path.Append(section).Append('/').Append(logId);

				if (!string.IsNullOrEmpty(argument))
				{
					path.Append('/').Append(argument);
				}
			}

			result.Path = path.ToString();
			return result.Uri;
		}

		private static string ToShortDateString(DateTime value)
		{
			// Force the short date to use a 2 digit year but without assuming anything about the current regional/cultural format.
			string result = value.ToShortDateString().Replace(value.Year.ToString(), (value.Year % 100).ToString());
			return result;
		}

		private void ClearDisplay()
		{
			this.title.Text = null;
			this.data.Children.Clear();
			this.footer.Text = null;
		}

		private void UpdateTitle()
		{
			string result = null;
			if (this.UseDefaultHeader)
			{
				switch (this.StatsFormat)
				{
					case StatsFormat.DistanceTotals:
						result = "Distance Totals";
						break;

					case StatsFormat.LatestWorkouts:
						result = "Latest Workouts";
						break;

					case StatsFormat.PersonalRecords:
						result = "Personal Records";
						break;

					case StatsFormat.ScheduledWorkouts:
						result = "Scheduled Workouts";
						break;
				}
			}
			else
			{
				result = this.Header;
			}

			this.title.Text = result;
		}

		private void UpdateFooter()
		{
			string result = null;

#pragma warning disable MEN013 // Use UTC time. This displays a local time to the user.
			DateTime now = DateTime.Now;
#pragma warning restore MEN013 // Use UTC time
			switch (this.FooterFormat)
			{
				case FooterFormat.RefreshedDateTime:
					result = ToShortDateString(now) + ' ' + now.ToShortTimeString();
					break;

				case FooterFormat.RefreshedTime:
					result = now.ToShortTimeString();
					break;
			}

			this.footer.Text = result;
		}

		private bool UpdateStats()
		{
			bool result = true;

			if (string.IsNullOrEmpty(this.LogId))
			{
				this.UpdateMessage("Please specify a RunningAhead Log ID in the Options.");
			}
			else
			{
				string statsPath;
				Action<XElement> updateAction;
				bool useRARecordsTable = true;
				switch (this.StatsFormat)
				{
					case StatsFormat.LatestWorkouts:
						statsPath = "latest";
						updateAction = this.UpdateLatestWorkouts;
						break;

					case StatsFormat.PersonalRecords:
						statsPath = "records";
						updateAction = this.UpdateTwoColumnStats;
						break;

					case StatsFormat.ScheduledWorkouts:
						statsPath = "scheduled_workouts";
						updateAction = this.UpdateScheduledWorkouts;
						useRARecordsTable = false;
						break;

					default:
						statsPath = "last";
						updateAction = this.UpdateTwoColumnStats;
						break;
				}

				Uri uri = GetRunningAheadUri("scripts", this.LogId, statsPath);
				if (GetHttpResponse(uri, out string response))
				{
					XElement xml = GetXhtmlData(response, useRARecordsTable);
					if (xml != null)
					{
						this.ClearDataGrid();
						updateAction(xml);
					}
					else
					{
						this.UpdateMessage("Unable to parse returned data as XML.");
					}
				}
				else
				{
					this.UpdateMessage(response);
					result = false;
				}
			}

			return result;
		}

		private void ClearDataGrid()
		{
			this.data.Children.Clear();
			this.data.ColumnDefinitions.Clear();
			this.data.RowDefinitions.Clear();
		}

		private void UpdateMessage(string message)
		{
			this.ClearDataGrid();
			TextBlock block = new TextBlock(new Run(message))
			{
				TextWrapping = TextWrapping.Wrap,
			};
			this.data.Children.Add(block);
		}

		private void UpdateTwoColumnStats(XElement xml)
		{
			XElement table = xml.Element("table");
			if (table != null)
			{
				// The second column should auto-size to allow long text like "Half Marathon:"
				// in the first column to be wider without wrapping.  This should work ok
				// unless someone takes over 99 hours (!) to complete their half marathon.
				this.data.ColumnDefinitions.Add(new ColumnDefinition());
				this.data.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

				int rowNumber = 0;
				foreach (XElement row in table.Elements("tr"))
				{
					this.data.RowDefinitions.Add(new RowDefinition());

					int columnNumber = 0;
					foreach (XElement cell in row.Elements())
					{
						TextBlock text = this.GetCellTextBlock(cell);
						text.HorizontalAlignment = columnNumber == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

						Grid.SetRow(text, rowNumber);
						Grid.SetColumn(text, columnNumber);

						this.data.Children.Add(text);
						columnNumber++;
					}

					rowNumber++;
				}
			}
		}

		private void UpdateLatestWorkouts(XElement xml)
		{
			XElement table = xml.Element("table");
			if (table != null)
			{
				this.data.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
				this.data.ColumnDefinitions.Add(new ColumnDefinition());
				this.data.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

				TextBlock dateText = null;
				int tableRowNumber = 0;
				foreach (XElement row in table.Elements("tr"))
				{
					if (tableRowNumber % 2 == 0)
					{
						XElement cell = row.Elements().FirstOrDefault();
						if (cell != null)
						{
							dateText = this.GetCellTextBlock(cell);
							if (DateTime.TryParse(cell.Value, out DateTime dateTime))
							{
								// Try to use a 2 digit year.
								dateText.Text = ToShortDateString(dateTime);
							}
						}
						else
						{
							// Something is out of whack, so we'll quit parsing.
							break;
						}
					}
					else
					{
						this.data.RowDefinitions.Add(new RowDefinition());
						int gridRowNumber = this.data.RowDefinitions.Count - 1;
						Grid.SetRow(dateText, gridRowNumber);
						this.data.Children.Add(dateText);

						int tableColumnNumber = 0;
						foreach (XElement cell in row.Elements())
						{
							int gridColumnNumber = tableColumnNumber + 1;
							TextBlock text = this.GetCellTextBlock(cell);
							text.HorizontalAlignment = HorizontalAlignment.Right;
							if (gridColumnNumber == 1)
							{
								// Leave a little separation between the "Activity" and "Distance" columns.
								const int RightGap = 5;
								text.Margin = new Thickness(0, 0, RightGap, 0);
							}

							Grid.SetRow(text, gridRowNumber);
							Grid.SetColumn(text, gridColumnNumber);

							this.data.Children.Add(text);
							tableColumnNumber++;
						}
					}

					tableRowNumber++;
				}
			}
		}

		private TextBlock GetCellTextBlock(XElement cell)
		{
			Inline inline = this.GetCellValueInline(cell);
			TextBlock text = new TextBlock(inline)
			{
				TextWrapping = TextWrapping.Wrap,
			};
			return text;
		}

		private Inline GetCellValueInline(XElement cell)
		{
			Inline result = new Run(cell.Value);

			XElement child = cell.Elements().FirstOrDefault();
			if (child != null && child.Name.LocalName == "a")
			{
				result = new Run(child.Value);
				string href = child.GetAttributeValue("href", null);
				if (!string.IsNullOrEmpty(href))
				{
					Hyperlink link = new Hyperlink(result)
					{
						NavigateUri = new Uri(href),
					};
					link.RequestNavigate += this.Hyperlink_RequestNavigate;
					result = link;
				}
			}

			return result;
		}

		private void UpdateScheduledWorkouts(XElement xml)
		{
			foreach (XElement element in xml.Elements())
			{
				TextBlock text = null;
				switch (element.Name.LocalName)
				{
					case "h4":
						text = this.GetCellTextBlock(element);
						text.FontWeight = FontWeights.Bold;
						break;

					case "p":
						text = this.GetCellTextBlock(element);
						break;
				}

				if (text != null)
				{
					this.data.RowDefinitions.Add(new RowDefinition());
					int rowNumber = this.data.RowDefinitions.Count - 1;
					this.data.Children.Add(text);
					Grid.SetRow(text, rowNumber);
				}
			}
		}

		#endregion

		#region Private Event Handlers

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			using (new WaitCursor())
			{
				WindowsUtility.ShellExecute(this, e.Uri.ToString());
			}
		}

		private void Refresh_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			using (new WaitCursor())
			{
				this.UpdateDisplay();
				e.Handled = true;
			}
		}

		private void ViewLog_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			using (new WaitCursor())
			{
				Uri uri = GetRunningAheadUri(LogsBasePath, this.LogId);
				WindowsUtility.ShellExecute(this, uri.ToString());
			}
		}

		#endregion
	}
}

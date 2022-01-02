namespace Menees.Gizmos.RunningAhead
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
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
	using Menees.Windows.Presentation;

	#endregion

	public sealed partial class StatsOptionsPage
	{
		#region Constructors

		public StatsOptionsPage(Stats stats)
		{
			this.InitializeComponent();
			this.Stats = stats;
		}

		#endregion

		#region Internal Properties

		internal Stats Stats { get; }

		#endregion

		#region Protected Methods

		protected override void OnInitialDisplay()
		{
			base.OnInitialDisplay();

			if (this.Stats != null)
			{
				this.logId.Text = this.Stats.LogId;
				this.hours.SelectedValue = this.Stats.RefreshInterval.TotalHours;
				this.useDefaultHeader.IsChecked = this.Stats.UseDefaultHeader;
				this.useCustomHeader.IsChecked = !this.useDefaultHeader.IsChecked;
				this.customHeader.Text = this.Stats.Header;
				this.statsFormat.SelectedValue = this.Stats.StatsFormat;
				this.footerFormat.SelectedValue = this.Stats.FooterFormat;

				// I couldn't get FocusManager.FocusedElement to work in XAML with
				// {Binding ElementName=logId} or with {x:Reference logId}.  Perhaps
				// it's because this UserControl isn't parented in a window until now.
				FocusManager.SetFocusedElement(this, this.logId);
			}
		}

		protected override bool OnOk()
		{
			bool result = base.OnOk();

			if (result && this.Stats != null)
			{
				string logId = this.logId.Text.Trim();
				if (!string.IsNullOrEmpty(logId))
				{
					string? errorMessage;
					if (!Guid.TryParseExact(logId, "N", out _))
					{
						errorMessage = "The Log ID must be exactly 32 characters long consisting of 0-9 and a-f.";
						this.logId.Focus();
					}
					else if (!Stats.ValidateLogId(logId, out errorMessage))
					{
						errorMessage = "The specified Log ID could not be validated at RunningAhead.com." + Environment.NewLine + errorMessage;
						this.logId.Focus();
					}

					if (errorMessage.IsNotEmpty())
					{
						WindowsUtility.ShowError(this, errorMessage);
						result = false;
					}
				}

				if (result)
				{
					using (new WaitCursor())
					{
						this.Stats.LogId = logId;
						this.Stats.RefreshInterval = TimeSpan.FromHours(GetValue(this.hours, this.Stats.RefreshInterval.TotalHours));
						this.Stats.UseDefaultHeader = this.useDefaultHeader.IsChecked ?? true;
						this.Stats.Header = this.customHeader.Text;
						this.Stats.StatsFormat = GetValue(this.statsFormat, this.Stats.StatsFormat);
						this.Stats.FooterFormat = GetValue(this.footerFormat, this.Stats.FooterFormat);

						this.Stats.UpdateDisplay();
					}
				}
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static T GetValue<T>(ComboBox comboBox, T defaultValue)
			where T : struct
		{
			T result = comboBox.SelectedValue as T? ?? defaultValue;
			return result;
		}

		#endregion
	}
}

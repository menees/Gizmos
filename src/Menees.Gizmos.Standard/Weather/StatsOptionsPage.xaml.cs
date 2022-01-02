namespace Menees.Gizmos.Weather
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

	public partial class StatsOptionsPage
	{
		#region Private Data Members

		private readonly Provider provider;

		#endregion

		#region Constructors

		public StatsOptionsPage(Stats stats)
		{
			this.InitializeComponent();
			this.Stats = stats;
			this.provider = Provider.Create();
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
				var settings = this.Stats.Settings;
				this.location.Text = settings.UserLocation;

				if (!this.provider.SupportsCityState)
				{
					this.locationLabel.Content = "_Location (US Zip code)";
				}

				if (this.provider.SupportsMetricUnits)
				{
					this.fahrenheit.IsChecked = settings.UseFahrenheit;
				}
				else
				{
					this.fahrenheit.IsChecked = true;
					this.fahrenheit.IsEnabled = false;
				}

				FocusManager.SetFocusedElement(this, this.location);
			}
		}

		protected override bool OnOk()
		{
			bool result = base.OnOk();

			if (result && this.Stats != null)
			{
				string location = this.location.Text;
				const int FirstSixDigitNumber = 100000;
				if (!this.provider.SupportsCityState && !int.TryParse(location, out int zip) && (zip <= 0 || zip >= FirstSixDigitNumber))
				{
					WindowsUtility.ShowError(this, location + " is not a valid US Zip code.");
					result = false;
				}
				else
				{
					var settings = new Settings(this.location.Text, this.fahrenheit.IsChecked.GetValueOrDefault());
					this.Stats.Settings = settings;
				}
			}

			return result;
		}

		#endregion
	}
}

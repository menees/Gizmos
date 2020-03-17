namespace Menees.Gizmos.Cpu
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

	#endregion

	public partial class StatsOptionsPage
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
				this.seconds.SelectedItem = this.Stats.RefreshInterval.TotalSeconds;
				this.top.ItemsSource = Enumerable.Range(0, Stats.MaxItems + 1);
				this.top.SelectedItem = this.Stats.TopCount;
				this.tenths.IsChecked = this.Stats.ShowTenths;
				this.zeros.IsChecked = this.Stats.ShowZeros;

				FocusManager.SetFocusedElement(this, this.seconds);
			}
		}

		protected override bool OnOk()
		{
			bool result = base.OnOk();

			if (result && this.Stats != null)
			{
				this.Stats.RefreshInterval = TimeSpan.FromSeconds((double)this.seconds.SelectedItem);
				this.Stats.TopCount = (int)this.top.SelectedItem;
				this.Stats.ShowTenths = this.tenths.IsChecked ?? false;
				this.Stats.ShowZeros = this.zeros.IsChecked ?? false;

				this.Stats.RefreshData();
			}

			return result;
		}

		#endregion
	}
}

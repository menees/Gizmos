namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Navigation;
	using System.Windows.Shapes;
	using System.Windows.Threading;

	#endregion

	[GizmoInfo(GizmoName = "Calendar", IsSingleInstance = true)]
	public partial class CalendarGizmo
	{
		#region Private Data Members

		private readonly DispatcherTimer timer;

		#endregion

		#region Constructors

		public CalendarGizmo()
		{
			this.InitializeComponent();
			this.SelectToday();

			this.timer = new DispatcherTimer
			{
				Interval = CalculateTimerInterval(),
			};
			this.timer.Tick += this.Timer_Tick;
			this.timer.IsEnabled = true;
		}

		#endregion

		#region Protected Methods

		protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
		{
			// The stupid Calendar control can capture the mouse and keep it too long, which makes
			// users have to click twice to get focus out of the control and on to another control.
			// http://stackoverflow.com/questions/2425951/wpf-toolkit-calendar-takes-two-clicks-to-get-focus
			base.OnPreviewMouseUp(e);

			if (Mouse.Captured is CalendarItem)
			{
				Mouse.Capture(null);
			}
		}

		#endregion

		#region Private Methods

		private static TimeSpan CalculateTimerInterval()
		{
			// We don't want to fire every second for an operation that's only needed
			// once per day.  So we'll just calculate the time until the next local midnight.
			// This is complicated by DST two days per year, so we'll convert to/from UTC.
			DateTime utcNow = DateTime.UtcNow;
			DateTime localNow = utcNow.ToLocalTime();
			DateTime nextLocalMidnight = localNow.Date.AddDays(1);
			DateTime nextLocalMidnightInUtc = nextLocalMidnight.ToUniversalTime();
			TimeSpan result = nextLocalMidnightInUtc - utcNow;
			return result;
		}

		private void SelectToday()
		{
#pragma warning disable MEN013 // Use UTC time. This displays a local date to the user.
			DateTime today = DateTime.Today;
#pragma warning restore MEN013 // Use UTC time
			this.calendar.SelectedDate = today;
			this.calendar.DisplayDate = today;
			this.calendar.DisplayMode = CalendarMode.Month;
			this.calendar.Focus();

			// Once the ToolTip has been shown, its Binding won't update again unless we explicitly poke it.
			// I also couldn't get the Binding's StringFormat to work to display the long date, so I ended up
			// with this because it's short and easy (and doesn't require calls to GetBindingExpression and
			// UpdateTarget).
			this.today.ToolTip = today.ToLongDateString();
		}

		#endregion

		#region Private Event Handlers

		private void Today_Click(object? sender, RoutedEventArgs e)
		{
			this.SelectToday();
		}

		private void Timer_Tick(object? sender, EventArgs e)
		{
			this.SelectToday();

			// Recalc the interval every day because it may not always be 24 hours.
			// Thanks to DST each year one day it will be 23 hours and one day it will be 25 hours.
			this.timer.IsEnabled = false;
			this.timer.Interval = CalculateTimerInterval();
			this.timer.IsEnabled = true;
		}

		private void TodayBar_Click(object? sender, MouseButtonEventArgs e)
		{
			this.DragGizmo();
		}

		#endregion
	}
}

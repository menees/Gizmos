namespace Menees.Gizmos.RunningAhead
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	#region internal StatsFormat

	internal enum StatsFormat
	{
		DistanceTotals,
		LatestWorkouts,
		PersonalRecords,
		ScheduledWorkouts,
	}

	#endregion

	#region internal FooterFormat

	internal enum FooterFormat
	{
		None,
		RefreshedTime,
		RefreshedDateTime,
	}

	#endregion
}

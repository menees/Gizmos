namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Xml.Linq;

	#endregion

	public sealed class ForecastInfo
	{
		#region Internal Fields

		/// <summary>
		/// This won't show up as a display line on the gizmo.
		/// </summary>
		internal static readonly ForecastInfo Empty = new ForecastInfo();

		/// <summary>
		/// This will show up as a "dummy" display line on the gizmo.
		/// </summary>
		internal static readonly ForecastInfo Missing = new ForecastInfo
		{
			Day = "---",
			HighLow = "--/--",
			PrecipitationText = "--",
			PrecipitationPercent = 0.5,
		};

		#endregion

		#region Private Data Members

		private string highLow;

		#endregion

		#region Constructors

		internal ForecastInfo()
		{
		}

		#endregion

		#region Public Properties

		public bool IsToday { get; internal set; }

		public string Day { get; internal set; }

		public Uri ImageUri { get; internal set; }

		public string Description { get; internal set; }

		public string HighLow
		{
			get
			{
				string result = this.highLow;

				if (string.IsNullOrEmpty(result))
				{
					result = (this.High != null ? this.High.ToString() : "?") + "/" + (this.Low != null ? this.Low.ToString() : "?");
				}

				return result;
			}

			set
			{
				this.highLow = value;
			}
		}

		public int? High { get; internal set; }

		public int? Low { get; internal set; }

		public string PrecipitationText { get; internal set; }

		public double PrecipitationPercent { get; internal set; }

		#endregion
	}
}

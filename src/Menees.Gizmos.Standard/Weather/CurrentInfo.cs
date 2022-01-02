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

	public sealed class CurrentInfo
	{
		#region Internal Fields

		internal static readonly CurrentInfo Missing = new()
		{
			Description = "-----------",
			FeelsLike = "--",
			Wind = "--",
			Humidity = "--",
		};

		#endregion

		#region Constructors

		internal CurrentInfo()
		{
		}

		#endregion

		#region Public Properties

		public Uri? ImageUri { get; internal set; }

		public int? TemperatureValue { get; internal set; }

		public string? TemperatureText { get; internal set; }

		public string? Description { get; internal set; }

		public string? Observed { get; internal set; }

		public string? FeelsLike { get; internal set; }

		public string? Wind { get; internal set; }

		public string? Humidity { get; internal set; }

		#endregion
	}
}

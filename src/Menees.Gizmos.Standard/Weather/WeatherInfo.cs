namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;

	#endregion

	public sealed class WeatherInfo
	{
		#region Private Data Members

		// The number of these should match how many forecast slots we have available on the gizmo.
		private static readonly ForecastInfo[] MissingForecasts = new[]
		{
			ForecastInfo.Missing,
			ForecastInfo.Empty,
			ForecastInfo.Empty,
			ForecastInfo.Empty,
			ForecastInfo.Empty,
		};

		private CurrentInfo current;
		private IReadOnlyList<ForecastInfo> dailyForecasts;

		#endregion

		#region Constructors

		public WeatherInfo()
			: this(null)
		{
			// This default constructor is only for use by the WPF designer
			// when using Format -> "Set Design-time DataContext...".
			Provider provider = Provider.Create();
			provider.UpdateWeatherAsync(this).Wait();
		}

		internal WeatherInfo(Settings settings)
		{
			this.Settings = settings ?? Settings.Default;
			this.LocationName = this.Settings.UserLocation;
		}

		#endregion

		#region Public Properties

		public Settings Settings { get; }

		public string LocationName { get; internal set; }

		public string Attribution { get; internal set; }

		public Uri ImageBaseUri { get; internal set; }

		public CurrentInfo Current
		{
			get
			{
				return this.current ?? CurrentInfo.Missing;
			}

			internal set
			{
				this.current = value;
			}
		}

		public IReadOnlyList<ForecastInfo> DailyForecasts
		{
			get
			{
				// Make sure our list contains forecasts for each display line.
				// The first one may be "Missing", and the rest can be "Empty".
				if (this.dailyForecasts == null || this.dailyForecasts.Count == 0)
				{
					this.dailyForecasts = MissingForecasts;
				}
				else if (this.dailyForecasts.Count < MissingForecasts.Length)
				{
					List<ForecastInfo> list = new List<ForecastInfo>(this.dailyForecasts);
					while (list.Count < MissingForecasts.Length)
					{
						list.Add(ForecastInfo.Empty);
					}

					this.dailyForecasts = list;
				}

				return this.dailyForecasts;
			}

			internal set
			{
				this.dailyForecasts = value;
			}
		}

		public string ErrorMessage { get; private set; }

		public bool IsValid
		{
			get
			{
				bool result = string.IsNullOrEmpty(this.ErrorMessage) && this.Current != null;
				return result;
			}
		}

		public string MoreInfoLabel => this.MoreInfoLink != null ? "More Information" : null;

		public Uri MoreInfoLink { get; internal set; }

		#endregion

		#region Public Methods

		public void SetError(string errorMessage)
		{
			// Keep the first error message that's set, but allow it to be cleared out.
			if (string.IsNullOrEmpty(this.ErrorMessage) || string.IsNullOrEmpty(errorMessage))
			{
				this.ErrorMessage = errorMessage;
			}

			this.Current = CurrentInfo.Missing;
			this.DailyForecasts = new[] { ForecastInfo.Missing };
		}

		#endregion
	}
}

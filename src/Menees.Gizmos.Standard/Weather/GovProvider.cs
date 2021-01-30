namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using System.Xml;
	using System.Xml.Linq;
	using System.Xml.XPath;

	#endregion

	internal sealed class GovProvider : Provider
	{
		#region Private Data Members

		private Dictionary<string, object> locationRequestParameters;

		#endregion

		#region Constructors

		public GovProvider()
		{
			this.SupportsCityState = false;
			this.SupportsMetricUnits = false;
			this.RequiresUserAgent = true;
		}

		#endregion

		#region Protected Methods

		protected override async Task UpdateAsync()
		{
			if (string.IsNullOrEmpty(this.Settings.UserLocation))
			{
				this.Weather.SetError("A US Zip code location must be specified.");
			}
			else
			{
				if (this.locationRequestParameters == null)
				{
					this.locationRequestParameters = await this.CacheLocationRequestParametersAsync().ConfigureAwait(false);
				}

				if (this.locationRequestParameters == null)
				{
					this.Weather.SetError("Unable to determine latitude and longitude.");
				}
				else
				{
					Uri weatherDataUri = BuildlUri("http://forecast.weather.gov/MapClick.php", this.locationRequestParameters);
					XElement weatherData = await this.GetXmlAsync(weatherDataUri).ConfigureAwait(false);
					if (this.IsDwml(weatherData))
					{
						var dataElements = weatherData.Elements("data");
						this.Weather.Current = this.GetCurrent(dataElements.FirstOrDefault(e => (string)e.Attribute("type") == "current observations"));
						this.Weather.DailyForecasts = this.GetForecasts(dataElements.FirstOrDefault(e => (string)e.Attribute("type") == "forecast"));

						// After mid-day, Weather.gov stops giving the high temp and icon forecast for today, so we'll get it from Current.
						ForecastInfo todaysForecast = this.Weather.DailyForecasts.FirstOrDefault(f => f.IsToday);
						if (todaysForecast != null && this.Weather.Current != CurrentInfo.Missing)
						{
							todaysForecast.ImageUri = this.Weather.Current.ImageUri;
							if (todaysForecast.High == null)
							{
								todaysForecast.High = this.Weather.Current.TemperatureValue;
								if (todaysForecast.High < todaysForecast.Low)
								{
									todaysForecast.Low = todaysForecast.High;
								}
							}
						}
					}
				}
			}
		}

		#endregion

		#region Private Methods

		private static Uri BuildlUri(string page, IDictionary<string, object> parameters)
		{
			UriBuilder builder = new UriBuilder(page)
			{
				Query = string.Join("&", parameters.Select(pair => string.Format("{0}={1}", pair.Key, pair.Value ?? pair.Key))),
			};
			Uri result = builder.Uri;
			return result;
		}

		private static string GetValue(XElement baseElement, string xpath)
		{
			string result = null;

			IEnumerable enumerable = baseElement.XPathEvaluate(xpath) as IEnumerable;
			XObject selectedObject = enumerable.OfType<XObject>().FirstOrDefault();
			if (selectedObject != null)
			{
				if (selectedObject is XElement selectedElement)
				{
					result = selectedElement.Value;
				}
				else
				{
					if (selectedObject is XAttribute selectedAttribute)
					{
						result = selectedAttribute.Value;
					}
					else
					{
						result = selectedObject.ToString();
					}
				}
			}

			return result;
		}

		private static Dictionary<Period, T> GetPeriodData<T>(
			XElement container,
			Dictionary<string, List<Period>> namedPeriods,
			string valueElementName = "value",
			Func<XElement, T> getValue = null)
		{
			Dictionary<Period, T> result = null;

			if (container != null)
			{
				string periodName = container.GetAttributeValue("time-layout", null);
				if (!string.IsNullOrEmpty(periodName) && namedPeriods.TryGetValue(periodName, out List<Period> periods))
				{
					var valueElements = container.Elements(valueElementName);
					if (valueElements.Count() == periods.Count)
					{
						result = new Dictionary<Period, T>(periods.Count);

						foreach (var pair in valueElements.Zip(periods, (item, period) => Tuple.Create(item, period)))
						{
							T value = getValue != null ? getValue(pair.Item1) : ConvertUtility.ConvertValue<T>(pair.Item1.Value);
							result.Add(pair.Item2, value);
						}
					}
				}
			}

			return result;
		}

		private static void SetForecastData<TIn, TOut>(
			Dictionary<DateTime, ForecastInfo> forecasts,
			Dictionary<Period, TIn> periodData,
			Func<IEnumerable<KeyValuePair<Period, TIn>>, TOut> getValue,
			Action<ForecastInfo, TOut> setValue)
		{
			if (periodData != null)
			{
				foreach (var dateGroup in periodData.GroupBy(pair => pair.Key.Start.Date))
				{
					DateTime date = dateGroup.Key;
					TOut value = getValue(dateGroup);

					if (!forecasts.TryGetValue(date, out ForecastInfo forecast))
					{
						forecast = new ForecastInfo
						{
							Day = date.ToString("ddd"),
#pragma warning disable MEN013 // Use UTC time. This compares against the user's local date.
							IsToday = date == DateTime.Today,
#pragma warning restore MEN013 // Use UTC time
						};
						forecasts.Add(date, forecast);
					}

					setValue(forecast, value);
				}
			}
		}

		private static bool TryGetLocalDateTime(string text, out DateTime value)
		{
			// Handle XML DateTimes according to http://www.w3.org/TR/xmlschema-2/#dateTime.
			// They'll be in a form like "2002-10-10T12:00:00-05:00" or "2002-10-10T12:00:00Z".
			// Using XmlConvert.ToDateTimeOffset will throw if the value can't be parsed, but
			// we can use DateTimeOffset.TryParse.
			bool result = DateTimeOffset.TryParse(text, out DateTimeOffset offsetValue);
			if (result)
			{
				// If the text was in UTC format (i.e., ending with 'Z'), then we need to explicitly convert to local.
				value = offsetValue.ToLocalTime().DateTime;
			}
			else
			{
				value = DateTime.MinValue;
			}

			return result;
		}

		private static Dictionary<string, List<Period>> GetNamedPeriods(XElement data)
		{
			Dictionary<string, List<Period>> result = new Dictionary<string, List<Period>>();

			foreach (XElement timeLayout in data.Elements("time-layout"))
			{
				XElement key = timeLayout.Element("layout-key");
				if (key != null)
				{
					string name = key.Value;
					if (!string.IsNullOrEmpty(name))
					{
						List<Period> periods = timeLayout.Elements("start-valid-time")
							.Select(e => new Period(e))
							.Where(p => p.IsValid)
							.ToList();
						result.Add(name, periods);
					}
				}
			}

			return result;
		}

		private async Task<Dictionary<string, object>> CacheLocationRequestParametersAsync()
		{
			var geocodeParameters = new Dictionary<string, object>
			{
				["text"] = this.Settings.UserLocation + ",+USA",
				["f"] = "json",
			};

			Uri geocodeUri = BuildlUri("https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/find", geocodeParameters);
			XElement geocode = await this.GetXmlAsync(geocodeUri, body => new XElement("Body", body)).ConfigureAwait(false);

			Dictionary<string, object> result = null;
			if (geocode != null)
			{
				// Targeting .NET 4.5 limits our JSON parsing options, so I'll just pull out the two values I need with a Regex.
				string json = geocode.Value.Replace(" ", string.Empty).Replace("\t", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
				Regex regex = new Regex(@"(?n)""geometry"":{""x"":(?<Long>-?\d+(\.\d+)?),""y"":(?<Lat>-?\d+(\.\d+)?)}");
				Match match = regex.Match(json);

				const int RequiredGroupCount = 3; // Whole pattern, Long, Lat
				if (match.Success
					&& match.Groups.Count == RequiredGroupCount
					&& decimal.TryParse(match.Groups[1].Value, out decimal longitude)
					&& decimal.TryParse(match.Groups[2].Value, out decimal latitude))
				{
					result = new Dictionary<string, object>
					{
						["lat"] = latitude,
						["lon"] = longitude,
						["unit"] = 0,
						["lg"] = "english",
						["FcstType"] = "dwml",
					};
				}
			}

			return result;
		}

		private bool IsDwml(XElement data)
		{
			if (data == null)
			{
				this.Weather.SetError("No weather data was available.");
			}
			else if (data.Name.LocalName != "dwml")
			{
				this.Weather.SetError(data.Value);
			}

			bool result = string.IsNullOrEmpty(this.Weather.ErrorMessage);
			return result;
		}

		private CurrentInfo GetCurrent(XElement data)
		{
			CurrentInfo result = CurrentInfo.Missing;

			if (data != null)
			{
				this.Weather.LocationName = GetValue(data, "location/area-description");
				this.Weather.Attribution = "Weather.gov";
				this.Weather.ImageBaseUri = null;

				result = new CurrentInfo
				{
					Observed = GetValue(data, "time-layout[@time-coordinate='local']/start-valid-time[@period-name='current']"),
				};
				if (TryGetLocalDateTime(result.Observed, out DateTime observed))
				{
					result.Observed = observed.ToShortTimeString();
				}

				XElement parameters = data.Element("parameters");
				if (parameters != null)
				{
					string temperatureRawText = GetValue(parameters, "temperature[@type='apparent']");
					if (int.TryParse(temperatureRawText, out int temperatureValue))
					{
						result.TemperatureText = this.BuildTemperature(temperatureRawText);
						result.TemperatureValue = temperatureValue;
					}

					result.Humidity = BuildPercentage(GetValue(parameters, "humidity"));
					result.Description = GetValue(parameters, "weather/weather-conditions/@weather-summary");

					string windValue = GetValue(parameters, "wind-speed[@type='sustained']");
					if (!string.IsNullOrEmpty(windValue) && int.TryParse(windValue, out int windSpeed))
					{
						string units = GetValue(parameters, "wind-speed[@type='sustained']/@units");
						if (units == "knots")
						{
							const decimal MilesPerKnot = 1.15077945m;
							windSpeed = (int)(MilesPerKnot * windSpeed);
							units = "mph";
						}

						result.Wind = windSpeed + " " + units;
					}

					string icon = GetValue(parameters, "conditions-icon/icon-link");
					if (!string.IsNullOrEmpty(icon) && Uri.TryCreate(icon, UriKind.Absolute, out Uri iconUri))
					{
						result.ImageUri = iconUri;
					}
				}
			}

			return result;
		}

		private IReadOnlyList<ForecastInfo> GetForecasts(XElement data)
		{
			List<ForecastInfo> result = new List<ForecastInfo>();

			if (data != null)
			{
				// The forecast location is usually better than the "current observations" location.
				string location = GetValue(data, "location/description");
				if (!string.IsNullOrEmpty(location))
				{
					this.Weather.LocationName = location;
				}

				string moreInfo = GetValue(data, "moreWeatherInformation");
				if (!string.IsNullOrEmpty(moreInfo) && Uri.TryCreate(moreInfo, UriKind.Absolute, out Uri moreInfoLink))
				{
					this.Weather.MoreInfoLink = moreInfoLink;
				}

				XElement parameters = data.Element("parameters");
				if (parameters != null)
				{
					Dictionary<string, List<Period>> namedPeriods = GetNamedPeriods(data);

					Dictionary<DateTime, ForecastInfo> forecasts = new Dictionary<DateTime, ForecastInfo>();
					var tempElements = parameters.Elements("temperature");
					var minTempPeriods = GetPeriodData<int?>(tempElements.FirstOrDefault(e => e.GetAttributeValue("type", null) == "minimum"), namedPeriods);
					SetForecastData(
						forecasts,
						minTempPeriods,
						pairs => pairs.Select(p => p.Value).Min(),
						(info, value) => info.Low = value);

					var maxTempPeriods = GetPeriodData<int?>(tempElements.FirstOrDefault(e => e.GetAttributeValue("type", null) == "maximum"), namedPeriods);
					SetForecastData(
						forecasts,
						maxTempPeriods,
						pairs => pairs.Select(p => p.Value).Max(),
						(info, value) => info.High = value);

					var rainPercentPeriods = GetPeriodData<int?>(parameters.Element("probability-of-precipitation"), namedPeriods);
					SetForecastData(
						forecasts,
						rainPercentPeriods,
						pairs => pairs.Select(p => p.Value).Max(),
						(info, value) =>
						{
							info.PrecipitationPercent = (value ?? 0) / 100.0;
							info.PrecipitationText = BuildPercentage((value ?? 0).ToString());
						});

					var longPeriodDescriptions = GetPeriodData(parameters.Element("wordedForecast"), namedPeriods, "text", e => e.Value);
					SetForecastData(
						forecasts,
						longPeriodDescriptions,
						pairs => pairs.Count() == 1
							? pairs.First().Value
							: string.Join(Environment.NewLine, pairs.OrderBy(p => p.Key.Start).Select(p => p.Key.Name + ": " + p.Value)),
						(info, value) => info.Description = value);

					var shortPeriodDescriptions = GetPeriodData(
						parameters.Element("weather"),
						namedPeriods,
						"weather-conditions",
						e => e.GetAttributeValue("weather-summary", null));
					SetForecastData(
						forecasts,
						shortPeriodDescriptions,
						pairs => pairs.Count() == 1
							? pairs.First().Value
							: string.Join(Environment.NewLine, pairs.OrderBy(p => p.Key.Start).Select(p => p.Key.Name + ": " + p.Value)),
						(info, value) =>
							{
								if (string.IsNullOrEmpty(info.Description))
								{
									info.Description = value;
								}
							});

					var periodIcons = GetPeriodData(
						parameters.Element("conditions-icon"),
						namedPeriods,
						"icon-link",
						e =>
							{
								Uri.TryCreate(e.Value, UriKind.Absolute, out Uri uri);
								return uri;
							});

#pragma warning disable MEN013 // Use UTC time. This compares against the user's local date.
					DateTime now = DateTime.Now;
#pragma warning restore MEN013 // Use UTC time
					SetForecastData(
						forecasts,
						periodIcons,
						pairs => pairs.Count() == 1
							? pairs.First().Value
							: (pairs.First().Key.Start.Date == now.Date
								? pairs.OrderBy(p => Math.Abs((p.Key.Start.TimeOfDay - now.TimeOfDay).TotalSeconds)).First().Value
								: pairs.OrderBy(p => p.Key.Start).First().Value),
						(info, value) => info.ImageUri = value);

					result.AddRange(forecasts.OrderBy(pair => pair.Key).Select(pair => pair.Value));
				}
			}

			return result;
		}

		#endregion

		#region Private Types

		private sealed class Period
		{
			#region Constructors

			public Period(XElement element)
			{
				this.Name = element.GetAttributeValue("period-name", null);
				if (!string.IsNullOrEmpty(this.Name) && TryGetLocalDateTime(element.Value, out DateTime value))
				{
					this.Start = value;
					this.IsValid = true;
				}
			}

			#endregion

			#region Public Properties

			public string Name { get; }

			public DateTime Start { get; }

			public bool IsValid { get; }

			#endregion
		}

		#endregion
	}
}

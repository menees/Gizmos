namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Net.Cache;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using System.Xml;
	using System.Xml.Linq;

	#endregion

	internal abstract partial class Provider
	{
		#region Public Properties

		public bool SupportsCityState { get; protected set; }

		public bool SupportsMetricUnits { get; protected set; }

		public bool RequiresUserAgent { get; protected set; }

		#endregion

		#region Protected Properties

		protected WeatherInfo Weather { get; private set; }

		protected Settings Settings => this.Weather.Settings;

		#endregion

		#region Public Methods

		public static Provider Create()
		{
			Provider result = new GovProvider();
			return result;
		}

		public async Task<WeatherInfo> GetWeatherAsync(Settings settings)
		{
			await this.UpdateWeatherAsync(new WeatherInfo(settings)).ConfigureAwait(false);
			return this.Weather;
		}

		public async Task UpdateWeatherAsync(WeatherInfo weather)
		{
			this.Weather = weather;
			await this.UpdateAsync().ConfigureAwait(false);
		}

		#endregion

		#region Protected Methods

		protected static string BuildPercentage(string value)
		{
			string result = null;

			if (!string.IsNullOrEmpty(value))
			{
				result = value + "%";
			}

			return result;
		}

		protected abstract Task UpdateAsync();

		protected string BuildTemperature(string value)
		{
			string result = null;

			if (!string.IsNullOrEmpty(value))
			{
				result = value + '\xB0' + (this.Settings.UseFahrenheit ? 'F' : 'C');
			}

			return result;
		}

		protected async Task<Tuple<XElement, string>> RequestXmlAsync(Uri requestUri)
		{
			XElement result = null;
			string errorMessage = null;

			string body = null;
			try
			{
				using (HttpClient client = CreateNonCachingHttpClient())
				{
					if (this.RequiresUserAgent)
					{
						client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; rv:11.0) like Gecko");
					}

					client.Timeout = Properties.Settings.Default.HttpRequestTimeout;
					body = await client.GetStringAsync(requestUri).ConfigureAwait(false);
					result = XElement.Parse(body);
				}
			}
			catch (HttpRequestException ex)
			{
				errorMessage = ex.Message;
			}
			catch (XmlException ex)
			{
				// If a DWML forecast isn't available, then Weather.gov returns a script that tries to change the window's URL.
				// That's useless for us, so we'll just display a custom message.  Also XElement.Parse can't parse it because
				// the URL contains an unescaped '&', which HTML allows but XML treats as an invalid entity.
				// <script language='javascript'>window.location.href='http://forecast.weather.gov/MapClick.php?zoneid=TNZ027&zflg=1'</script>
				if (body != null && body.StartsWith("<script language='javascript'>window.location.href=") && body.EndsWith("</script>"))
				{
					errorMessage = "Digital weather information is not currently available.";
				}
				else
				{
					errorMessage = ex.Message;
				}
			}
			catch (TaskCanceledException)
			{
				// Give a better message than "A task was canceled." for HTTP timeouts.
				errorMessage = "The request timed out.";
			}

			return Tuple.Create(result, errorMessage);
		}

		protected async Task<XElement> GetXmlAsync(Uri requestUri)
		{
			var tuple = await this.RequestXmlAsync(requestUri).ConfigureAwait(false);
			XElement result = tuple.Item1;
			if (!string.IsNullOrEmpty(tuple.Item2))
			{
				this.Weather.SetError(tuple.Item2);
			}

			return result;
		}

		#endregion
	}
}

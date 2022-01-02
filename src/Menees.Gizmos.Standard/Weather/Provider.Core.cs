namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Net.Http;
	using System.Text;

	#endregion

	internal abstract partial class Provider
	{
		#region Private Methods

		private static HttpClient CreateNonCachingHttpClient()
		{
			// .NET Core doesn't support WebRequestHandler, but it's default caching
			// behavior is equivalent to RequestCacheLevel.BypassCache.
			// https://stackoverflow.com/a/44060627/1882616
			HttpClient result = new();
			return result;
		}

		#endregion
	}
}

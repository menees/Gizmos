namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Net.Cache;
	using System.Net.Http;
	using System.Text;

	#endregion

	internal abstract partial class Provider
	{
		#region Private Methods

		private static HttpClient CreateNonCachingHttpClient()
		{
#pragma warning disable CA2000 // Dispose objects before losing scope. This is disposed by the HttpClient below.
			WebRequestHandler handler = new WebRequestHandler
			{
				// We always want the most up-to-date info, so don't use cached responses.
				CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache),
			};
#pragma warning restore CA2000 // Dispose objects before losing scope

			HttpClient result = new HttpClient(handler, disposeHandler: true);
			return result;
		}

		#endregion
	}
}

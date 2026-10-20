using System.Net;
using Microsoft.Net.Http.Headers;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Applies a <see cref="CookieContainer"/> to requests sent through the in-memory test server.
    /// The default <see cref="WebApplicationFactory{TEntryPoint}"/> client keeps its cookies in a
    /// private handler, so a test that needs to replay an old cookie cannot reach them.
    /// </summary>
    internal sealed class CookieContainerHandler : DelegatingHandler
    {
        private readonly CookieContainer _cookies;

        public CookieContainerHandler(HttpMessageHandler innerHandler, CookieContainer cookies)
            : base(innerHandler)
        {
            _cookies = cookies;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                var cookieHeader = _cookies.GetCookieHeader(request.RequestUri);
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    request.Headers.TryAddWithoutValidation(HeaderNames.Cookie, cookieHeader);
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (request.RequestUri is not null
                && response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
            {
                foreach (var value in values)
                {
                    try
                    {
                        _cookies.SetCookies(request.RequestUri, value);
                    }
                    catch (CookieException)
                    {
                        // A malformed cookie is a server bug, but it must not turn a response a test
                        // wants to inspect into an exception from the transport.
                    }
                }
            }

            return response;
        }
    }
}

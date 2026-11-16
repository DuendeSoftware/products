// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Net.Http.Headers;

namespace Duende.Xunit.Playwright;

public class CookieHandler : DelegatingHandler
{
    private readonly CookieContainer _cookieContainer;

    public CookieHandler(HttpMessageHandler innerHandler, CookieContainer cookieContainer)
        : base(innerHandler) => _cookieContainer = cookieContainer;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var requestUri = request.RequestUri;
        var header = _cookieContainer.GetCookieHeader(requestUri!);
        if (!string.IsNullOrEmpty(header))
        {
            request.Headers.Add(HeaderNames.Cookie, header);
        }

        var response = await base.SendAsync(request, ct);

        if (response.Headers.TryGetValues(HeaderNames.SetCookie, out var setCookieHeaders))
        {
            foreach (var cookieHeader in SetCookieHeaderValue.ParseList(setCookieHeaders.ToList()))
            {
                var cookie = new Cookie(cookieHeader.Name.Value!,
                    cookieHeader.Value.Value,
                    cookieHeader.Path.Value);
                if (cookieHeader.Expires.HasValue)
                {
                    cookie.Expires = cookieHeader.Expires.Value.UtcDateTime;
                }

                _cookieContainer.Add(requestUri!, cookie);
            }
        }

        return response;
    }
}

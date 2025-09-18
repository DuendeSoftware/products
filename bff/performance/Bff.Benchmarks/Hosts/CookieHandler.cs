// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Net.Http.Headers;

namespace Bff.Benchmarks.Hosts;

internal class CookieHandler(HttpMessageHandler innerHandler, CookieContainer cookieContainer)
    : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CT ct)
    {
        var requestUri = request.RequestUri;
        var header = cookieContainer.GetCookieHeader(requestUri!);
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

                cookie.Secure = cookieHeader.Secure;
                cookie.HttpOnly = cookieHeader.HttpOnly;

                cookieContainer.Add(requestUri!, cookie);
            }
        }

        return response;
    }
}

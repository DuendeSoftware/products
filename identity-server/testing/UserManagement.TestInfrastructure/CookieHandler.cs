// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Net.Http.Headers;

namespace Duende.UserManagement;

/// <summary>
/// HTTP handler that manages cookies across requests and optionally follows redirects.
/// <para>
/// When <paramref name="allowAutoRedirect"/> is <c>true</c>, redirects are handled here
/// (not by <see cref="HttpClientHandler"/>), so every redirect request flows back through
/// the full handler chain — ensuring cookies set on intermediate 30x responses are captured.
/// The inner handler's <see cref="HttpClientHandler.AllowAutoRedirect"/> must be <c>false</c>
/// to avoid double-redirect handling.
/// </para>
/// </summary>
internal class CookieHandler(HttpMessageHandler innerHandler, CookieContainer? cookies = null, bool allowAutoRedirect = false) : DelegatingHandler(innerHandler)
{
    private const int MaxRedirects = 20;

    public void ClearCookies() => CookieContainer = new CookieContainer();
    public CookieContainer CookieContainer { get; private set; } = cookies ?? new CookieContainer();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Ct ct)
    {
        var response = await SendWithCookiesAsync(request, ct);

        var redirectCount = 0;
        // Track whether we created the current request (i.e. it is not the
        // caller-owned original) so we can dispose it after re-use.
        var ownsRequest = false;
        while (allowAutoRedirect &&
               (int)response.StatusCode is >= 300 and < 400 &&
               response.Headers.Location is not null)
        {
            if (++redirectCount > MaxRedirects)
            {
                throw new InvalidOperationException(
                    $"Too many redirects ({redirectCount}).");
            }

            var location = response.Headers.Location;
            if (!location.IsAbsoluteUri)
            {
                location = new Uri(request.RequestUri!, location);
            }

            // Dispose the intermediate response to avoid leaking connections.
            response.Dispose();

            // Dispose any intermediate request we created on a prior
            // iteration (never the caller-owned original).
            if (ownsRequest)
            {
                request.Dispose();
            }

            // Build a fresh GET request so the redirect flows back through
            // the full handler chain and cookies are captured.
#pragma warning disable CA2000 // run dispose on this request in the next loop iteration or after the final response is received
            request = new HttpRequestMessage(HttpMethod.Get, location);
#pragma warning restore CA2000
            ownsRequest = true;
            response = await SendWithCookiesAsync(request, ct);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendWithCookiesAsync(
        HttpRequestMessage request, Ct ct)
    {
        var requestUri = request.RequestUri;
        var header = CookieContainer.GetCookieHeader(requestUri!);
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

                CookieContainer.Add(requestUri!, cookie);
            }
        }

        return response;
    }
}

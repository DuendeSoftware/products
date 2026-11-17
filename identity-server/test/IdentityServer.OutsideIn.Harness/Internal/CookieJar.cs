// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>A cookie as a <c>Set-Cookie</c> header described it.</summary>
internal sealed record SetCookie(
    string Name,
    string Value,
    string? Domain,
    string Path,
    bool Secure,
    bool HttpOnly,
    string? SameSite,
    DateTimeOffset? Expires)
{
    public static SetCookie? Parse(string header, Uri requestUri, DateTimeOffset now)
    {
        var parts = header.Split(';');
        var nameValue = parts[0].Split('=', 2);
        if (nameValue.Length != 2 || nameValue[0].Trim().Length == 0)
        {
            return null;
        }

        string? domain = null;
        var path = DefaultPath(requestUri);
        var secure = false;
        var httpOnly = false;
        string? sameSite = null;
        DateTimeOffset? expires = null;
        DateTimeOffset? maxAgeExpires = null;

        foreach (var attribute in parts.Skip(1))
        {
            var pair = attribute.Split('=', 2);
            var key = pair[0].Trim().ToLowerInvariant();
            var value = pair.Length == 2 ? pair[1].Trim() : "";
            switch (key)
            {
                case "domain":
                    domain = value.TrimStart('.');
                    break;
                case "path" when value.StartsWith('/'):
                    path = value;
                    break;
                case "secure":
                    secure = true;
                    break;
                case "httponly":
                    httpOnly = true;
                    break;
                case "samesite":
                    sameSite = value.ToLowerInvariant();
                    break;
                case "expires" when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date):
                    expires = date;
                    break;
                case "max-age" when long.TryParse(value, CultureInfo.InvariantCulture, out var seconds):
                    maxAgeExpires = seconds <= 0 ? DateTimeOffset.MinValue : now.AddSeconds(seconds);
                    break;
                default:
                    break;
            }
        }

        return new SetCookie(nameValue[0].Trim(), nameValue[1].Trim(), domain, path, secure, httpOnly, sameSite, maxAgeExpires ?? expires);
    }

    private static string DefaultPath(Uri uri)
    {
        var path = uri.AbsolutePath;
        var lastSlash = path.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : path[..lastSlash];
    }
}

/// <summary>
/// The browser's cookie jar. Unlike <see cref="System.Net.CookieContainer" />, it decides expiry with the test clock,
/// so cookies expire when the test advances the clock, and it applies SameSite rules the way a browser does.
/// </summary>
internal sealed class CookieJar(TestClock clock)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<(string Host, string Path, string Name), SetCookie> _cookies = [];

    public void Store(Uri requestUri, SetCookie cookie)
    {
        var key = (cookie.Domain ?? requestUri.Host, cookie.Path, cookie.Name);
        lock (_lock)
        {
            if (cookie.Expires is { } expires && expires <= clock.TimeProvider.GetUtcNow())
            {
                _cookies.Remove(key);
            }
            else
            {
                _cookies[key] = cookie;
            }
        }
    }

    /// <summary>The <c>Cookie</c> header for a request, or <c>null</c> when no cookie applies.</summary>
    /// <param name="requestUri">Where the request goes.</param>
    /// <param name="method">The request method. Cross-site POSTs don't carry SameSite=Lax cookies.</param>
    /// <param name="initiator">The page that caused the request (a redirect or a form), or <c>null</c> when the test sent it.</param>
    public string? GetCookieHeader(Uri requestUri, HttpMethod method, Uri? initiator)
    {
        var now = clock.TimeProvider.GetUtcNow();
        var crossSite = initiator is not null && !string.Equals(initiator.Host, requestUri.Host, StringComparison.OrdinalIgnoreCase);

        lock (_lock)
        {
            var cookies = _cookies
                .Where(pair => HostMatches(pair.Key.Host, pair.Value.Domain is not null, requestUri.Host)
                    && PathMatches(pair.Value.Path, requestUri.AbsolutePath)
                    && (pair.Value.Expires is null || pair.Value.Expires > now)
                    && (!pair.Value.Secure || requestUri.Scheme == Uri.UriSchemeHttps)
                    && SameSiteAllows(pair.Value.SameSite, crossSite, method))
                .OrderByDescending(pair => pair.Value.Path.Length)
                .Select(pair => $"{pair.Value.Name}={pair.Value.Value}")
                .ToList();
            return cookies.Count == 0 ? null : string.Join("; ", cookies);
        }
    }

    private static bool HostMatches(string cookieHost, bool isDomainCookie, string requestHost) =>
        string.Equals(cookieHost, requestHost, StringComparison.OrdinalIgnoreCase)
        || (isDomainCookie && requestHost.EndsWith("." + cookieHost, StringComparison.OrdinalIgnoreCase));

    private static bool PathMatches(string cookiePath, string requestPath) =>
        requestPath == cookiePath
        || (requestPath.StartsWith(cookiePath, StringComparison.Ordinal)
            && (cookiePath.EndsWith('/') || requestPath[cookiePath.Length] == '/'));

    // A cookie without SameSite is treated as Lax, as browsers do.
    private static bool SameSiteAllows(string? sameSite, bool crossSite, HttpMethod method) =>
        sameSite switch
        {
            "none" => true,
            "strict" => !crossSite,
            _ => !crossSite || method == HttpMethod.Get,
        };
}

/// <summary>Adds the jar's cookies to every request and stores the cookies of every response.</summary>
internal sealed class CookieJarHandler(CookieJar jar, TestClock clock, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <summary>The page that caused a request. Set by the browser's flow helpers; absent when the test sent the request.</summary>
    public static readonly HttpRequestOptionsKey<Uri> Initiator = new("OutsideIn.Initiator");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Ct cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("The TestBrowser needs absolute request URIs.");
        request.Options.TryGetValue(Initiator, out var initiator);
        if (jar.GetCookieHeader(uri, request.Method, initiator) is { } cookieHeader)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            foreach (var header in headers)
            {
                if (SetCookie.Parse(header, uri, clock.TimeProvider.GetUtcNow()) is { } cookie)
                {
                    jar.Store(uri, cookie);
                }
            }
        }

        return response;
    }
}

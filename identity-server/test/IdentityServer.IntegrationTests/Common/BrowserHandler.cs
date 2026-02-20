// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;

namespace Duende.IdentityServer.IntegrationTests.Common;

// thanks to Damian Hickey for this awesome sample
// https://github.com/damianh/OwinHttpMessageHandler/blob/master/src/OwinHttpMessageHandler/OwinHttpMessageHandler.cs
public class BrowserHandler : DelegatingHandler
{
    private CookieContainer _cookieContainer = new CookieContainer();

    public bool AllowCookies { get; set; } = true;
    public bool AllowAutoRedirect { get; set; } = true;
    public int ErrorRedirectLimit { get; set; } = 20;
    public int StopRedirectingAfter { get; set; } = int.MaxValue;

    public BrowserHandler(HttpMessageHandler next)
        : base(next)
    {
    }

    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CT ct)
    {
        var response = await SendCookiesAsync(request, ct);

        var redirectCount = 0;

        while (AllowAutoRedirect &&
               (300 <= (int)response.StatusCode && (int)response.StatusCode < 400) &&
               redirectCount < StopRedirectingAfter)
        {
            if (redirectCount >= ErrorRedirectLimit)
            {
                throw new InvalidOperationException(string.Format("Too many redirects. Error limit = {0}", redirectCount));
            }

            var location = response.Headers.Location;
            if (!location.IsAbsoluteUri)
            {
                location = new Uri(response.RequestMessage.RequestUri, location);
            }

            request = new HttpRequestMessage(HttpMethod.Get, location);

            response = await SendCookiesAsync(request, ct).ConfigureAwait(false);

            redirectCount++;
        }

        return response;
    }

    internal Cookie GetCookie(string uri, string name) => _cookieContainer.GetCookies(new Uri(uri)).FirstOrDefault(x => x.Name == name);

    internal void RemoveCookie(string uri, string name)
    {
        var cookie = _cookieContainer.GetCookies(new Uri(uri)).FirstOrDefault(x => x.Name == name);
        if (cookie != null)
        {
            cookie.Expired = true;
        }
    }

    protected async Task<HttpResponseMessage> SendCookiesAsync(HttpRequestMessage request, CT ct)
    {
        if (AllowCookies)
        {
            var cookieHeader = _cookieContainer.GetCookieHeader(request.RequestUri);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Add("Cookie", cookieHeader);
            }
        }

        var response = await base.SendAsync(request, ct);

        if (AllowCookies && response.Headers.Contains("Set-Cookie"))
        {
            var responseCookieHeader = string.Join(',', response.Headers.GetValues("Set-Cookie"));
            _cookieContainer.SetCookies(request.RequestUri, responseCookieHeader);
        }

        return response;
    }
}

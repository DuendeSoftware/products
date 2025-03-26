// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;

namespace Duende.Bff.Tests.TestFramework;

public class TestBrowserClient : HttpClient
{
    private class CookieHandler(HttpMessageHandler next) : DelegatingHandler(next)
    {
        public CookieContainer CookieContainer { get; } = new();
        public Uri? CurrentUri { get; private set; }
        public HttpResponseMessage? LastResponse { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CurrentUri = request.RequestUri ?? throw new NullReferenceException("RequestUri is not set");
            var cookieHeader = CookieContainer.GetCookieHeader(request.RequestUri);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Add("Cookie", cookieHeader);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.Headers.Contains("Set-Cookie"))
            {
                var responseCookieHeader = string.Join(',', response.Headers.GetValues("Set-Cookie"));
                CookieContainer.SetCookies(request.RequestUri, responseCookieHeader);
            }

            LastResponse = response;

            return response;
        }
    }

    private readonly CookieHandler _handler;

    private CookieContainer CookieContainer => _handler.CookieContainer;
    public Uri? CurrentUri => _handler.CurrentUri;
    public HttpResponseMessage? LastResponse => _handler.LastResponse;

    public TestBrowserClient(HttpMessageHandler handler)
        : this(new CookieHandler(handler))
    {
    }

    private TestBrowserClient(CookieHandler handler)
        : base(handler)
    {
        _handler = handler;
    }

    public void RemoveCookie(string name)
    {
        if (CurrentUri == null) throw new NullReferenceException("CurrentUri is null");

        RemoveCookie(CurrentUri.ToString(), name);
    }

    private void RemoveCookie(string uri, string name)
    {
        var cookie = CookieContainer.GetCookies(new Uri(uri)).FirstOrDefault(x => x.Name == name);
        if (cookie != null)
        {
            cookie.Expired = true;
        }
    }


    /// <summary>
    /// Calls the specified BFF api and verifies that the response is successful
    /// and returns the ApiResponse object.
    /// </summary>
    /// <param name="url">The url to call</param>
    /// <param name="expectedStatusCode">If specified, the system will verify that this reponse code was given</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The specified api response</returns>
    public async Task<BffHostResponse> CallBffHostApi(
        string url,
        HttpStatusCode? expectedStatusCode = null,
        CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-csrf", "1");
        var response = await SendAsync(req, ct);

        if (expectedStatusCode == null)
        {
            response.IsSuccessStatusCode.ShouldBeTrue();
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
            var json = await response.Content.ReadAsStringAsync(ct);
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json).ShouldNotBeNull();

            apiResult.Method.ShouldBe("GET", StringCompareShould.IgnoreCase);

            return new(response, apiResult);
        }
        else
        {
            response.StatusCode.ToString().ShouldBe(expectedStatusCode.ToString());
            return new(response, null!);
        }
    }

    public async Task<BffHostResponse> CallBffHostApi(
        string url,
        HttpMethod method,
        HttpContent? content = null,
        HttpStatusCode? expectedStatusCode = null,
        CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(method, url);
        if (req.Content == null)
        {
            req.Content = content;
        }

        req.Headers.Add("x-csrf", "1");
        var response = await SendAsync(req, ct);
        if (expectedStatusCode == null)
        {
            response.IsSuccessStatusCode.ShouldBeTrue();
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
            var json = await response.Content.ReadAsStringAsync(ct);
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json).ShouldNotBeNull();

            apiResult.Method.ShouldBe(method.ToString(), StringCompareShould.IgnoreCase);
            return new(response, apiResult);
        }
        else
        {
            response.StatusCode.ToString().ShouldBe(expectedStatusCode.ToString());
            return new(response, null!);
        }
    }

    public record BffHostResponse(HttpResponseMessage HttpResponse, ApiResponse ApiResponse)
    {
        public static implicit operator HttpResponseMessage(BffHostResponse response) => response.HttpResponse;
        public static implicit operator ApiResponse(BffHostResponse response) => response.ApiResponse;
    }
}

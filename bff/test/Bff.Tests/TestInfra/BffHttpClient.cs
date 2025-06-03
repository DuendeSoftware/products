// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Duende.Bff.Tests.TestFramework;

namespace Duende.Bff.Tests.TestInfra;

public static class CookieContainerExtensions
{
    public static void Clear(this CookieContainer container, Uri uri)
    {
        var cookies = container.GetCookies(uri);
        foreach (var cookie in cookies.ToArray())
        {
            container.SetCookies(uri, $"{cookie.Name}=; path={cookie.Path}; expires=Thu, 01 Jan 1970 00:00:00 GMT; {(cookie.Secure ? "Secure;" : "")} HttpOnly;");
        }
    }
}

public class BffHttpClient(RedirectHandler handler, CookieContainer cookies) : HttpClient(handler), IHttpClient<BffHttpClient>
{
    public CookieContainer Cookies { get; } = cookies;

    public RedirectHandler RedirectHandler = handler;

    public async Task<HttpResponseMessage> Login(PathString? basePath = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK) => await GetAsync(basePath.ToString() + "/bff/login")
        .CheckHttpStatusCode(expectedStatusCode);


    public static BffHttpClient Build(RedirectHandler handler, CookieContainer cookies) => new(handler, cookies);


    internal Task<TestBrowserClient.BffHostResponse> CallBffHostApi(
        PathString path,
        HttpMethod? method = null,
        HttpContent? content = null,
        HttpStatusCode? expectedStatusCode = null,
        Dictionary<string, string>? headers = null,
        CT ct = default) => CallBffHostApi(
        url: new Uri(path, UriKind.Relative),
        method: method,
        content: content,
        expectedStatusCode: expectedStatusCode,
        headers: headers,
        ct: ct);

    internal async Task<TestBrowserClient.BffHostResponse> CallBffHostApi(
        Uri url,
        HttpMethod? method = null,
        HttpContent? content = null,
        HttpStatusCode? expectedStatusCode = null,
        Dictionary<string, string>? headers = null,
        CT ct = default)
    {
        method ??= HttpMethod.Get;
        var req = new HttpRequestMessage(method, url);
        if (req.Content == null)
        {
            req.Content = content;
        }

        expectedStatusCode ??= HttpStatusCode.OK;

        req.Headers.Add("x-csrf", "1");

        foreach (var header in headers ?? [])
        {
            req.Headers.Add(header.Key, header.Value);
        }

        var response = await SendAsync(req, ct);
        response.StatusCode.ShouldBe(expectedStatusCode.Value);

        if (expectedStatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
            var json = await response.Content.ReadAsStringAsync(ct);
            var apiResult = JsonSerializer.Deserialize<ApiCallDetails>(json).ShouldNotBeNull();

            apiResult.Method.ShouldBe(method);
            return new(response, apiResult);
        }
        else
        {
            return new(response, null!);
        }
    }

    public async Task<bool> GetIsUserLoggedInAsync(string? userQuery = null)
    {
        if (userQuery != null)
        {
            userQuery = "?" + userQuery;
        }

        var req = new HttpRequestMessage(HttpMethod.Get, "/bff/user" + userQuery);
        req.Headers.Add("x-csrf", "1");
        var response = await SendAsync(req);

        (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Unauthorized)
            .ShouldBeTrue();

        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task CreateIdentityServerSessionCookieAsync(IdentityServerTestHost host, string sub, string? sid = null)
    {
        host.PropsToSignIn = new();

        if (!string.IsNullOrWhiteSpace(sid))
        {
            host.PropsToSignIn.Items.Add("session_id", sid);
        }

        await IssueSessionCookieAsync(host, new Claim("sub", sub));
    }

    public async Task IssueSessionCookieAsync(IdentityServerTestHost host, params Claim[] claims)
    {
        var previousUser = host.UserToSignIn;
        try
        {
            host.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"));
            var response = await GetAsync(host.Url("__signin"));
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        finally
        {
            host.UserToSignIn = previousUser;
        }

    }
    public async Task RevokeIdentityServerSession(Uri url) => await GetAsync(new Uri(url, "__signout")).CheckHttpStatusCode(HttpStatusCode.NoContent);
}

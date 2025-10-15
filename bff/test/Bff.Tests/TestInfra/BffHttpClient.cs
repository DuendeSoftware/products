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

public class BffHttpClient(RedirectHandler handler, CookieContainer cookies, IdentityServerTestHost identityServer) : HttpClient(handler)
{
    public CookieContainer Cookies { get; } = cookies;

    public RedirectHandler RedirectHandler = handler;

    public async Task<HttpResponseMessage> Login(PathString? basePath = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK) => await GetAsync(basePath.ToString() + "/bff/login")
        .CheckHttpStatusCode(expectedStatusCode);



    public async Task<List<JsonRecord>> CallUserEndpointAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        req.Headers.Add("x-csrf", "1");

        var response = await SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<JsonRecord>>(json, TestSerializerOptions.Default) ?? [];
    }

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

        if (headers == null)
        {
            req.Headers.Add("x-csrf", "1");
        }

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

    public async Task<bool> GetIsUserLoggedInAsync(string? userQuery = null, string? basePath = null)
    {
        if (userQuery != null)
        {
            userQuery = "?" + userQuery;
        }

        var req = new HttpRequestMessage(HttpMethod.Get, basePath + "/bff/user" + userQuery);
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

        await IssueSessionCookieAsync(new Claim("sub", sub));
    }

    public async Task IssueSessionCookieAsync(params Claim[] claims)
    {
        var previousUser = identityServer.UserToSignIn;
        try
        {
            identityServer.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"));
            var response = await GetAsync(identityServer.Url("__signin"));
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        finally
        {
            identityServer.UserToSignIn = previousUser;
        }

    }
    public async Task RevokeIdentityServerSession() => await GetAsync(identityServer.Url("__signout")).CheckHttpStatusCode(HttpStatusCode.NoContent);

    public async Task<HttpResponseMessage> Logout(string? sid = null, Uri? returnUrl = null)
    {
        sid ??= await GetSid();

        var returnParams = returnUrl == null ? null : $"&returnUrl={Uri.EscapeDataString(returnUrl.ToString())}";

        var req = new HttpRequestMessage(HttpMethod.Get, "/bff/logout?sid=" + sid + returnParams);
        req.Headers.Add("x-csrf", "1");
        return await SendAsync(req);
    }

    public async Task<string> GetSid()
    {
        var claims = await CallUserEndpointAsync();

        var sidClaim = claims.FirstOrDefault(c => c.Type == "sid")?.Value;
        sidClaim.ShouldNotBeNull();
        var sid = sidClaim.Value.ToString();
        return sid;
    }
}

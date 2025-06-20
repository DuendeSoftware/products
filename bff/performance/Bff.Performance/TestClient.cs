// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bff.Performance.TestInfra;

namespace Bff.Performance;

public class TestClient(Uri baseAddress, CookieHandler cookies, HttpMessageHandler handler)
{

    public HttpClient Client = new(handler)
    {
        BaseAddress = baseAddress
    };
    public void ClearCookies() => cookies.ClearCookies();

    public static TestClient Create(Uri baseAddress, CookieContainer? cookies = null)
    {
        var inner = new SocketsHttpHandler
        {
            // We need to disable cookies and follow redirects
            // because we do this manually (see below). 
            UseCookies = false,
            AllowAutoRedirect = false
        };

        var cookieHandler = new CookieHandler(inner, cookies);
        var handler = new AutoFollowRedirectHandler((_) => { })
        {
            InnerHandler = cookieHandler
        };

        return new TestClient(baseAddress, cookieHandler, handler);
    }

    public Task<HttpResponseMessage> GetAsync(string path, Dictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        var request = BuildRequest(HttpMethod.Get, path, headers);

        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string path, object? body, Dictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        var request = BuildRequest(HttpMethod.Post, path, headers);
        request.Content = JsonContent.Create(body);

        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync<T>(Uri path, T body, Dictionary<string, string>? headers = null, CancellationToken ct = default)
        where T : HttpContent
    {
        var request = BuildRequest(HttpMethod.Post, path.ToString(), headers);
        request.Content = body;

        return Client.SendAsync(request);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod httpMethod, string path,
        Dictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(httpMethod, path);

        if (headers == null)
        {
            request.Headers.Add("x-csrf", "1");
        }
        else
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        return request;
    }


    public async Task<HttpResponseMessage> TriggerLogin(string userName = "alice", string password = "alice", CancellationToken ct = default)
    {
        return await GetAsync("/bff/login");
    }

    public async Task<HttpResponseMessage> TriggerLogout()
    {
        // To trigger a logout, we need the logout claim
        var userClaims = await GetUserClaims();

        var logoutLink = userClaims.FirstOrDefault(x => x.Type == "bff:logout_url")
                         ?? throw new InvalidOperationException("Failed to find logout link claim");

        return await GetAsync(logoutLink.Value.ToString()!);
    }

    public async Task<UserClaim[]> GetUserClaims()
    {
        var userClaimsString = await GetStringAsync("/bff/user");
        var userClaims = JsonSerializer.Deserialize<UserClaim[]>(userClaimsString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
        return userClaims;
    }

    private async Task<string> GetStringAsync(string path)
    {
        var response = await GetAsync(path);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to get string from {path}. Status code: {response.StatusCode}");
        }
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<HttpResponseMessage> InvokeApi(string url) => await GetAsync(url);

    public record UserClaim
    {
        public required string Type { get; init; }
        public required object Value { get; init; }
    }
}

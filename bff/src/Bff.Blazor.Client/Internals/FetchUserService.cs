// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Blazor.Client.Internals;

/// <summary>
/// Internal service that retrieves user info from the /bff/user endpoint.
/// </summary>
internal class FetchUserService : IDisposable
{
    private readonly HttpClient _client;
    private readonly ILogger<FetchUserService> _logger;

    /// <summary>
    /// Internal service that retrieves user info from the /bff/user endpoint.
    /// </summary>
    /// <param name="clientFactory"></param>
    /// <param name="logger"></param>
    public FetchUserService(IHttpClientFactory clientFactory,
        ILogger<FetchUserService> logger)
    {
        _logger = logger;
        _client = clientFactory.CreateClient(BffClientAuthenticationStateProvider.HttpClientName);
    }

    /// <summary>
    /// Parameterless ctor for testing only.
    /// </summary>
    internal FetchUserService()
    {
        _client = new HttpClient();
#pragma warning disable CA2000 // This is a test-only ctor, so we don't want to dispose the client here.
        _logger = new Logger<FetchUserService>(new LoggerFactory());
#pragma warning restore CA2000
    }

    public virtual async ValueTask<ClaimsPrincipal> FetchUserAsync()
    {
        try
        {
            _logger.FetchingUserInformation();
            var claims = await _client.GetFromJsonAsync<List<ClaimRecord>>("bff/user?slide=false");

            var identity = new ClaimsIdentity(
                nameof(BffClientAuthenticationStateProvider),
                "name",
                "role");

            if (claims != null)
            {
                foreach (var claim in claims)
                {
                    identity.AddClaim(new Claim(claim.Type, claim.Value.ToString() ?? "no value"));
                }
            }

            return new ClaimsPrincipal(identity);
        }
        catch (HttpRequestException ex)
        {
            _logger.FetchingUserFailed(ex);
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
        catch (JsonException ex)
        {
            _logger.FetchingUserFailed(ex);
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    public void Dispose() => _client.Dispose();
}

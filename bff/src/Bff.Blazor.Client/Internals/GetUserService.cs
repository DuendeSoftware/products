// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Blazor.Client.Internals;

/// <summary>
/// Internal service that retrieves user info from the /bff/user endpoint.
/// </summary>
internal class GetUserService
{
    private readonly HttpClient _client;
    private readonly ILogger<GetUserService> _logger;

    /// <summary>
    /// Internal service that retrieves user info from the /bff/user endpoint.
    /// </summary>
    /// <param name="clientFactory"></param>
    /// <param name="logger"></param>
    internal GetUserService(IHttpClientFactory clientFactory,
        ILogger<GetUserService> logger)
    {
        _logger = logger;
        _client = clientFactory.CreateClient(BffClientAuthenticationStateProvider.HttpClientName);
    }

    /// <summary>
    /// Parameterless ctor for testing only.
    /// </summary>
    internal GetUserService()
    {
        _client = new HttpClient();
        _logger = new Logger<GetUserService>(new LoggerFactory());
    }

    public virtual async ValueTask<ClaimsPrincipal> GetUserAsync()
    {
        try
        {
            _logger.LogInformation("Fetching user information.");
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fetching user failed.");
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Blazor.Client.Internals;

internal class GetUserService : IGetUserService
{
    private readonly HttpClient _client;
    private readonly IPersistentUserService _persistentUserService;
    private readonly TimeProvider _timeProvider;
    private readonly BffBlazorOptions _options;
    private readonly ILogger<GetUserService> _logger;
    
    private DateTimeOffset _userLastCheck = DateTimeOffset.MinValue;
    private ClaimsPrincipal _cachedUser = new(new ClaimsIdentity());

    public GetUserService(
        IHttpClientFactory clientFactory, 
        IPersistentUserService persistentUserService,
        TimeProvider timeProvider,
        IOptions<BffBlazorOptions> options,
        ILogger<GetUserService> logger)
    {
        _client = clientFactory.CreateClient(BffClientAuthenticationStateProvider.HttpClientName);
        _persistentUserService = persistentUserService;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public void InitializeCache()
    {
        _cachedUser = _persistentUserService.GetPersistedUser();
        if (_cachedUser.Identity?.IsAuthenticated == true)
        {
            _userLastCheck = _timeProvider.GetUtcNow();
        }
    }
    
    public async ValueTask<ClaimsPrincipal> GetUserAsync(bool useCache = true)
    {
        var now = _timeProvider.GetUtcNow();
        if (useCache && now < _userLastCheck.AddMilliseconds(_options.WebAssemblyStateProviderPollingDelay))
        {
            _logger.LogDebug("Taking user from cache");
            return _cachedUser;
        }

        _logger.LogDebug("Fetching user");
        _cachedUser = await FetchUser();
        _userLastCheck = now;

        return _cachedUser;
    }
    
    internal async Task<ClaimsPrincipal> FetchUser()
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
        }

        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}
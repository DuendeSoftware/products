// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Blazor.Client.Internals;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Blazor.Client;

internal class BffClientAuthenticationStateProvider : AuthenticationStateProvider
{
    public const string HttpClientName = "Duende.Bff.Blazor.Client:StateProvider";
    
    private readonly FetchUserService _fetchUserService;
    private readonly PersistentUserService _persistentUserService;
    private readonly TimeProvider _timeProvider;
    private readonly BffBlazorOptions _options;
    private readonly ITimer? _timer;
    private ClaimsPrincipal _user;
    private readonly ILogger<BffClientAuthenticationStateProvider> _logger;

    /// <summary>
    /// An <see cref="AuthenticationStateProvider"/> intended for use in Blazor
    /// WASM. It polls the /bff/user endpoint to monitor session state.
    /// </summary>
    public BffClientAuthenticationStateProvider(FetchUserService fetchUserService,
        PersistentUserService persistentUserService,
        TimeProvider timeProvider,
        IOptions<BffBlazorOptions> options,
        ILogger<BffClientAuthenticationStateProvider> logger)
    {
        _fetchUserService = fetchUserService;
        _persistentUserService = persistentUserService;
        _timeProvider = timeProvider;
        _options = options.Value;
        _timer = _timeProvider.CreateTimer(TimerCallback,
            null,
            TimeSpan.FromMilliseconds(_options.WebAssemblyStateProviderPollingDelay),
            TimeSpan.FromMilliseconds(_options.WebAssemblyStateProviderPollingInterval));
        _logger = logger;
        _user = _persistentUserService.GetPersistedUser(out var user)
            ? user
            : new ClaimsPrincipal(new ClaimsIdentity());
    }
    
    private async void TimerCallback(object? _)
    {
        // We don't want to do any polling if we already know that the user is anonymous.
        // There's no way for us to become authenticated without the server issuing a cookie
        // and that can't happen while the WASM code is running.
        
        if (_user is { Identity.IsAuthenticated: true })
        {
            var currentUser = await _fetchUserService.FetchUserAsync();
            // Always notify that auth state has changed, because the user
            // management claims (usually) change over time. 
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(currentUser)));

            // If the session ended, then we can stop polling
            if (currentUser.Identity!.IsAuthenticated == false)
            {
                if (_timer != null)
                {
                    await _timer.DisposeAsync();
                }
            }
        }
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_user));
    }
}
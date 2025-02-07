// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Blazor.Client.Internals;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Blazor.Client;

public class BffClientAuthenticationStateProvider : AuthenticationStateProvider
{
    public const string HttpClientName = "Duende.Bff.Blazor.Client:StateProvider";
    
    private readonly IGetUserService _getUserService;
    private readonly TimeProvider _timeProvider;
    private readonly BffBlazorOptions _options;
    private readonly ILogger<BffClientAuthenticationStateProvider> _logger;

    /// <summary>
    /// An <see cref="AuthenticationStateProvider"/> intended for use in Blazor
    /// WASM. It polls the /bff/user endpoint to monitor session state.
    /// </summary>
    public BffClientAuthenticationStateProvider(
        IGetUserService getUserService,
        TimeProvider timeProvider,
        IOptions<BffBlazorOptions> options,
        ILogger<BffClientAuthenticationStateProvider> logger)
    {
        _getUserService = getUserService;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _getUserService.InitializeCache();
        var user = await _getUserService.GetUserAsync();
        var state = new AuthenticationState(user);

        if (user.Identity is  { IsAuthenticated: true })
        {
            _logger.LogInformation("starting background check..");
            ITimer? timer = null;

            async void TimerCallback(object? _)
            {
                var currentUser = await _getUserService.GetUserAsync(false);
                // Always notify that auth state has changed, because the user
                // management claims (usually) change over time. 
                //
                // Future TODO - Someday we may want an extensibility point. If the
                // user management claims have been customized, then auth state
                // might not always change. In that case, we'd want to only fire
                // if the user actually had changed.
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(currentUser)));

                if (currentUser!.Identity!.IsAuthenticated == false)
                {
                    _logger.LogInformation("user logged out");

                    if (timer != null)
                    {
                        await timer.DisposeAsync();
                    }
                }
            }

            timer = _timeProvider.CreateTimer(TimerCallback, 
            null, 
            TimeSpan.FromMilliseconds(_options.WebAssemblyStateProviderPollingDelay), 
            TimeSpan.FromMilliseconds(_options.WebAssemblyStateProviderPollingInterval));
        }
        return state;
    }
}

/// <summary>
/// Constants for Duende.BFF
/// </summary>
public static class Constants
{
    /// <summary>
    /// Custom claim types used by Duende.BFF
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// Claim type for logout URL including session id
        /// </summary>
        public const string LogoutUrl = "bff:logout_url";
    }
}

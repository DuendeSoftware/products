// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Security.Claims;
using Duende.Bff.Blazor.Client;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// This is based on the PersistingServerAuthenticationStateProvider from ASP.NET
// 8's templates.

// Future TODO - In .NET 9, the types added by the template are getting moved
// into ASP.NET itself, so we could potentially extend those instead of copying
// the template.

namespace Duende.Bff.Blazor;


// This is a server-side AuthenticationStateProvider that uses
// PersistentComponentState to flow the authentication state to the client which
// is then used to initialize the authentication state in the WASM application. 
public sealed class BffServerAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider, IDisposable
{
    private readonly IUserSessionStore _sessionStore;
    private readonly PersistentComponentState _state;
    private readonly NavigationManager _navigation;
    private readonly ILogger<BffServerAuthenticationStateProvider> _logger;

    private readonly PersistingComponentStateSubscription _subscription;

    private Task<AuthenticationState>? _authenticationStateTask;

    protected override TimeSpan RevalidationInterval { get; }

    public BffServerAuthenticationStateProvider(
        IUserSessionStore sessionStore,
        PersistentComponentState persistentComponentState,
        NavigationManager navigation,
        IOptions<BffBlazorOptions> options,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _sessionStore = sessionStore;
        _state = persistentComponentState;
        _navigation = navigation;
        _logger = loggerFactory.CreateLogger<BffServerAuthenticationStateProvider>();

        // TODO - Consider separate options for server and client
        RevalidationInterval = TimeSpan.FromMilliseconds(options.Value.StateProviderPollingInterval);

        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = _state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _authenticationStateTask = task;
    }

    private async Task OnPersistingAsync()
    {
        if (_authenticationStateTask is null)
        {
            throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");
        }

        var authenticationState = await _authenticationStateTask;

        var claims = authenticationState.User.Claims
            .Select(c => new ClaimRecord
            {
                Type = c.Type,
                Value = c.Value?.ToString() ?? string.Empty,
                ValueType = c.ValueType == ClaimValueTypes.String ? null : c.ValueType
            }).ToArray();

        var principal = new ClaimsPrincipalRecord
        {
            AuthenticationType = authenticationState.User.Identity!.AuthenticationType,
            NameClaimType = authenticationState.User.Identities.First().NameClaimType,
            RoleClaimType = authenticationState.User.Identities.First().RoleClaimType,
            Claims = claims
        };

        _logger.LogDebug("Persisting Authentication State");

        _state.PersistAsJson(nameof(ClaimsPrincipalRecord), principal);
    }


    public void Dispose()
    {
        _subscription.Dispose();
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var sid = authenticationState.User.FindFirstValue(JwtClaimTypes.SessionId);
        var sub = authenticationState.User.FindFirstValue(JwtClaimTypes.Subject);

        var sessions = await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter
        {
            SessionId = sid,
            SubjectId = sub
        });
        return sessions.Count != 0;
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Security.Claims;
using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Duende.Bff.Licensing;
using Duende.Bff.SessionManagement.SessionStore;
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

/// <summary>
/// This is a server-side AuthenticationStateProvider that uses
/// PersistentComponentState to flow the authentication state to the client which
/// is then used to initialize the authentication state in the WASM application. 
/// </summary>
internal sealed class BffServerAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider, IDisposable
{
    private readonly IUserSessionStore _sessionStore;
    private readonly PersistentComponentState _state;
    private readonly NavigationManager _navigation;
    private readonly BuildUserSessionPartitionKey _buildUserSessionPartitionKey;
    private readonly BffOptions _bffOptions;
    private readonly ILogger<BffServerAuthenticationStateProvider> _logger;

    private readonly PersistingComponentStateSubscription _subscription;

    private Task<AuthenticationState>? _authenticationStateTask;

    protected override TimeSpan RevalidationInterval { get; }

    public BffServerAuthenticationStateProvider(
        IUserSessionStore sessionStore,
        PersistentComponentState persistentComponentState,
        NavigationManager navigation,
        BuildUserSessionPartitionKey buildUserSessionPartitionKey,
        IOptions<BffBlazorServerOptions> blazorOptions,
        IOptions<BffOptions> bffOptions,
        LicenseValidator licenseValidator,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _sessionStore = sessionStore;
        _state = persistentComponentState;
        _navigation = navigation;
        _buildUserSessionPartitionKey = buildUserSessionPartitionKey;
        _bffOptions = bffOptions.Value;
        _logger = loggerFactory.CreateLogger<BffServerAuthenticationStateProvider>();

        RevalidationInterval = TimeSpan.FromMilliseconds(blazorOptions.Value.ServerStateProviderPollingInterval);

        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = _state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);

        CheckLicense(licenseValidator);
    }


    internal static void CheckLicense(LicenseValidator validator)
    {
        if (!validator.IsValid())
        {
            // todo: license enforcement
        }
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => _authenticationStateTask = task;

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
                Value = c.Value?.ToString() ?? string.Empty
            }).ToList();


        if (claims.All(x => x.Type != Constants.ClaimTypes.LogoutUrl))
        {
            var sessionId = authenticationState.User.FindFirst(JwtClaimTypes.SessionId)?.Value;
            claims.Add(new ClaimRecord(
                Constants.ClaimTypes.LogoutUrl,
                LogoutUrlBuilder.Build(_navigation, _bffOptions, sessionId)));
        }

        var principal = new ClaimsPrincipalRecord
        {
            AuthenticationType = authenticationState.User.Identity!.AuthenticationType,
            NameClaimType = authenticationState.User.Identities.First().NameClaimType,
            RoleClaimType = authenticationState.User.Identities.First().RoleClaimType,
            Claims = claims.ToArray()
        };

        _logger.PersistingAuthenticationState(LogLevel.Debug);

        _state.PersistAsJson(nameof(ClaimsPrincipalRecord), principal);
    }


    /// <inheritdoc />
    public void Dispose()
    {
        _subscription.Dispose();
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }

    /// <summary>
    /// Validates the current authentication state by checking if the user session exists in the session store.
    /// </summary>
    /// <param name="authenticationState">The current authentication state.</param>
    /// <param name="ct">A token that can be used to request cancellation of the asynchronous operation.</param>
    /// <returns>A boolean indicating whether the authentication state is valid.</returns>
    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CT ct)
    {
        var sid = authenticationState.User.FindFirstValue(JwtClaimTypes.SessionId);
        var sub = authenticationState.User.FindFirstValue(JwtClaimTypes.Subject);

        var userSessionsFilter = new UserSessionsFilter
        {
            SessionId = sid,
            SubjectId = sub
        };
        var partitionKey = _buildUserSessionPartitionKey();

        var sessions = await _sessionStore.GetUserSessionsAsync(partitionKey, userSessionsFilter, ct);
        return sessions.Count != 0;
    }
}

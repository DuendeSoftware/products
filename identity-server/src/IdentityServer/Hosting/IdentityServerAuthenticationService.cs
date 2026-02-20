// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

// this decorates the real authentication service to detect when the
// user is being signed in. this allows us to ensure the user has
// the claims needed for identity server to do its job. it also allows
// us to track signin/signout so we can issue/remove the session id
// cookie used for check session iframe for session management spec.
// finally, we track if signout is called to collaborate with the
// FederatedSignoutAuthenticationHandlerProvider for federated signout.
internal class IdentityServerAuthenticationService : IAuthenticationService
{
    private readonly IAuthenticationService _inner;
    private readonly IAuthenticationSchemeProvider _schemes;
    private readonly TimeProvider _timeProvider;
    private readonly IUserSession _session;
    private readonly IIssuerNameService _issuerNameService;
    private readonly ISessionCoordinationService _sessionCoordinationService;
    private readonly ILogger<IdentityServerAuthenticationService> _logger;

    public IdentityServerAuthenticationService(
        Decorator<IAuthenticationService> decorator,
        IAuthenticationSchemeProvider schemes,
        TimeProvider timeProvider,
        IUserSession session,
        IIssuerNameService issuerNameService,
        ISessionCoordinationService sessionCoordinationService,
        ILogger<IdentityServerAuthenticationService> logger)
    {
        _inner = decorator.Instance;

        _schemes = schemes;
        _timeProvider = timeProvider;
        _session = session;
        _issuerNameService = issuerNameService;
        _sessionCoordinationService = sessionCoordinationService;
        _logger = logger;
    }

    public async Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal, AuthenticationProperties properties)
    {
        var defaultScheme = await _schemes.GetDefaultSignInSchemeAsync();
        var cookieScheme = await context.GetCookieAuthenticationSchemeAsync();

        if ((scheme == null && defaultScheme?.Name == cookieScheme) || scheme == cookieScheme)
        {
            AugmentPrincipal(principal);

            properties ??= new AuthenticationProperties();
            await _session.CreateSessionIdAsync(principal, properties);
        }

        await _inner.SignInAsync(context, scheme, principal, properties);
    }

    private void AugmentPrincipal(ClaimsPrincipal principal)
    {
        _logger.LogDebug("Augmenting SignInContext");

        AssertRequiredClaims(principal);
        AugmentMissingClaims(principal, _timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task SignOutAsync(HttpContext context, string scheme, AuthenticationProperties properties)
    {
        var defaultScheme = await _schemes.GetDefaultSignOutSchemeAsync();
        var cookieScheme = await context.GetCookieAuthenticationSchemeAsync();

        if ((scheme == null && defaultScheme?.Name == cookieScheme) || scheme == cookieScheme)
        {
            // this sets a flag used by middleware to do post-signout work.
            context.SetSignOutCalled();

            if (!context.GetBackChannelLogoutTriggered())
            {
                // Note: it is important the work for triggering back-channel logout
                // is inside the Response.OnStarting event. Otherwise, in some conditions
                // the request will never complete.
                // See: https://github.com/DuendeArchive/IdentityServer4/issues/4644
                context.Response.OnStarting(async () =>
                {
                    _logger.LogDebug("SignOutCalled set; processing post-signout session cleanup.");

                    // back channel logout
                    var user = await _session.GetUserAsync();
                    if (user != null)
                    {
                        var session = new UserSession
                        {
                            SubjectId = user.GetSubjectId(),
                            SessionId = await _session.GetSessionIdAsync(),
                            DisplayName = user.GetDisplayName(),
                            ClientIds = (await _session.GetClientListAsync()).ToList(),
                            Issuer = await _issuerNameService.GetCurrentAsync()
                        };
                        await _sessionCoordinationService.ProcessLogoutAsync(session, context.RequestAborted);
                    }

                    // this clears our session id cookie so JS clients can detect the user has signed out
                    await _session.RemoveSessionIdCookieAsync();
                });

                context.SetBackChannelLogoutTriggered();
            }
        }

        await _inner.SignOutAsync(context, scheme, properties);
    }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme) => _inner.AuthenticateAsync(context, scheme);

    public Task ChallengeAsync(HttpContext context, string scheme, AuthenticationProperties properties) => _inner.ChallengeAsync(context, scheme, properties);

    public Task ForbidAsync(HttpContext context, string scheme, AuthenticationProperties properties) => _inner.ForbidAsync(context, scheme, properties);

    private static void AssertRequiredClaims(ClaimsPrincipal principal)
    {
        // for now, we don't allow more than one identity in the principal/cookie
        if (principal.Identities.Count() != 1)
        {
            throw new InvalidOperationException("only a single identity supported");
        }

        if (principal.FindFirst(JwtClaimTypes.Subject) == null)
        {
            throw new InvalidOperationException("sub claim is missing");
        }
    }

    private void AugmentMissingClaims(ClaimsPrincipal principal, DateTime authTime)
    {
        var identity = principal.Identities.First();

        // ASP.NET Identity issues this claim type and uses the authentication middleware name
        // such as "Google" for the value. this code is trying to correct/convert that for
        // our scenario. IOW, we take their old AuthenticationMethod value of "Google"
        // and issue it as the idp claim. we then also issue a amr with "external"
        var amr = identity.FindFirst(ClaimTypes.AuthenticationMethod);
        if (amr != null &&
            identity.FindFirst(JwtClaimTypes.IdentityProvider) == null &&
            identity.FindFirst(JwtClaimTypes.AuthenticationMethod) == null)
        {
            _logger.LogDebug("Removing amr claim with value: {value}", amr.Value);
            identity.RemoveClaim(amr);

            _logger.LogDebug("Adding idp claim with value: {value}", amr.Value);
            identity.AddClaim(new Claim(JwtClaimTypes.IdentityProvider, amr.Value));

            _logger.LogDebug("Adding amr claim with value: {value}", Constants.ExternalAuthenticationMethod);
            identity.AddClaim(new Claim(JwtClaimTypes.AuthenticationMethod, Constants.ExternalAuthenticationMethod));
        }

        if (identity.FindFirst(JwtClaimTypes.IdentityProvider) == null)
        {
            _logger.LogDebug("Adding idp claim with value: {value}", IdentityServerConstants.LocalIdentityProvider);
            identity.AddClaim(new Claim(JwtClaimTypes.IdentityProvider, IdentityServerConstants.LocalIdentityProvider));
        }

        if (identity.FindFirst(JwtClaimTypes.AuthenticationMethod) == null)
        {
            if (identity.FindFirst(JwtClaimTypes.IdentityProvider).Value == IdentityServerConstants.LocalIdentityProvider)
            {
                _logger.LogDebug("Adding amr claim with value: {value}", OidcConstants.AuthenticationMethods.Password);
                identity.AddClaim(new Claim(JwtClaimTypes.AuthenticationMethod, OidcConstants.AuthenticationMethods.Password));
            }
            else
            {
                _logger.LogDebug("Adding amr claim with value: {value}", Constants.ExternalAuthenticationMethod);
                identity.AddClaim(new Claim(JwtClaimTypes.AuthenticationMethod, Constants.ExternalAuthenticationMethod));
            }
        }

        if (identity.FindFirst(JwtClaimTypes.AuthenticationTime) == null)
        {
            var time = new DateTimeOffset(authTime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

            _logger.LogDebug("Adding auth_time claim with value: {value}", time);
            identity.AddClaim(new Claim(JwtClaimTypes.AuthenticationTime, time, ClaimValueTypes.Integer64));
        }
    }
}

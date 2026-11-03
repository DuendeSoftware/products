// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Internal;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Endpoints.Internal;

// This decorates the real authentication service to detect when Challenge or Forbid is being called
// for a BFF API endpoint, so it can translate the resulting redirect into a 401/403 response instead.
// Authenticate/SignIn/SignOut are pure pass-throughs: scheme remapping for "no scheme specified" /
// default-scheme calls is handled by BffAuthenticationSchemeProvider, which resolves the frontend's
// own scheme for the default authenticate/sign-in/forbid/challenge/sign-out scheme methods.
internal class BffAuthenticationService(Decorator<IAuthenticationService> decorator,
    CurrentFrontendAccessor currentFrontendAccessor,
    ILogger<BffAuthenticationService> logger)
    : IAuthenticationService
{
    private readonly IAuthenticationService _inner = decorator.Instance;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => _inner.SignInAsync(context, scheme, principal, properties);

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => _inner.SignOutAsync(context, scheme, properties);

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => _inner.AuthenticateAsync(context, scheme);

    public async Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        if (!ShouldHandleBffApiResponse(context))
        {
            await _inner.ChallengeAsync(context, scheme, properties);
            return;
        }

        if (!IsKnownBffScheme(scheme))
        {
            await _inner.ChallengeAsync(context, scheme, properties);
            if (context.Response.StatusCode != StatusCodes.Status302Found)
            {
                return;
            }
        }

        logger.ChallengeForBffApiEndpoint(LogLevel.Debug);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        RemoveChallengeResponseHeaders(context);
    }

    public async Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        if (!ShouldHandleBffApiResponse(context))
        {
            await _inner.ForbidAsync(context, scheme, properties);
            return;
        }

        if (!IsKnownBffScheme(scheme))
        {
            await _inner.ForbidAsync(context, scheme, properties);
            if (context.Response.StatusCode != StatusCodes.Status302Found)
            {
                return;
            }
        }

        logger.ForbidForBffApiEndpoint(LogLevel.Debug);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        RemoveChallengeResponseHeaders(context);
    }

    private static bool ShouldHandleBffApiResponse(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var isBffEndpoint = endpoint?.Metadata.GetMetadata<IBffApiMetadata>() != null;
        var skipResponseHandling = endpoint?.Metadata.GetMetadata<IBffApiSkipResponseHandling>() != null;
        return isBffEndpoint && !skipResponseHandling;
    }

    private bool IsKnownBffScheme(string? scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return true;
        }

        if (scheme == BffAuthenticationSchemes.BffCookie.ToString() || scheme == BffAuthenticationSchemes.BffOpenIdConnect.ToString())
        {
            return true;
        }

        if (!currentFrontendAccessor.TryGet(out var frontend))
        {
            return false;
        }

        return scheme == frontend.CookieSchemeName.ToString() || scheme == frontend.OidcSchemeName.ToString();
    }

    private static void RemoveChallengeResponseHeaders(HttpContext context)
    {
        _ = context.Response.Headers.Remove("Location");
        _ = context.Response.Headers.Remove("Set-Cookie");
    }
}

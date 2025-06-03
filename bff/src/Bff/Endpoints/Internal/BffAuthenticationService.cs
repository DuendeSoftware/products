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

// this decorates the real authentication service to detect when
// Challenge of Forbid is being called for a BFF API endpoint
internal class BffAuthenticationService(Decorator<IAuthenticationService> decorator,
    SelectedFrontend selectedFrontend,
    ILogger<BffAuthenticationService> logger)
    : IAuthenticationService
{
    private readonly IAuthenticationService _inner = decorator.Instance;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => _inner.SignInAsync(context, scheme, principal, properties);

    public async Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        if (selectedFrontend.TryGet(out var frontend))
        {
            if (scheme == frontend.CookieSchemeName.ToString() || scheme == frontend.OidcSchemeName.ToString())
            {
                await _inner.SignOutAsync(context, scheme, properties);
                return;
            }

            // Todo: EV: not sure.
            logger.LogWarning("Authentiating scheme: {scheme}", scheme);
            await _inner.SignOutAsync(context, frontend.OidcSchemeName, properties);
            return;
        }

        await _inner.SignOutAsync(context, scheme, properties);
    }

    public async Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
    {
        if (selectedFrontend.TryGet(out var frontend))
        {
            if (scheme == frontend.CookieSchemeName.ToString() || scheme == frontend.OidcSchemeName.ToString())
            {
                return await _inner.AuthenticateAsync(context, scheme);
            }

            // Todo: EV: not sure.
            // It looks like all schemes are authentiated, even if we only want the frontend scheme to be triggered.
            // Force the cookie scheme to be authentiated. LIkely this means it happens twice
            logger.LogWarning("Authentiating scheme: {scheme}", scheme);
            return await _inner.AuthenticateAsync(context, frontend.CookieSchemeName);
        }

        return await _inner.AuthenticateAsync(context, scheme);
    }

    public async Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        await _inner.ChallengeAsync(context, scheme, properties);

        if (context.Response.StatusCode != 302)
        {
            return;
        }

        var endpoint = context.GetEndpoint();

        var isBffEndpoint = endpoint?.Metadata.GetMetadata<IBffApiMetadata>() != null;
        if (!isBffEndpoint)
        {
            return;
        }

        var requireResponseHandling = endpoint?.Metadata.GetMetadata<IBffApiSkipResponseHandling>() == null;
        if (requireResponseHandling)
        {
            logger.ChallengeForBffApiEndpoint();
            context.Response.StatusCode = 401;
            context.Response.Headers.Remove("Location");
            context.Response.Headers.Remove("Set-Cookie");
        }
    }

    public async Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        await _inner.ForbidAsync(context, scheme, properties);

        if (context.Response.StatusCode != 302)
        {
            return;
        }

        var endpoint = context.GetEndpoint();

        var isBffEndpoint = endpoint?.Metadata.GetMetadata<IBffApiMetadata>() != null;
        if (!isBffEndpoint)
        {
            return;
        }

        var requireResponseHandling = endpoint?.Metadata.GetMetadata<IBffApiSkipResponseHandling>() == null;
        if (requireResponseHandling)
        {
            logger.ForbidForBffApiEndpoint();
            context.Response.StatusCode = 403;
            context.Response.Headers.Remove("Location");
            context.Response.Headers.Remove("Set-Cookie");
        }
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.EndpointProcessing;

// this decorates the real authentication service to detect when
// Challenge of Forbid is being called for a BFF API endpoint
internal class BffAuthenticationService(
    Decorator<IAuthenticationService> decorator,
    ILogger<BffAuthenticationService> logger)
    : IAuthenticationService
{
    private readonly IAuthenticationService _inner = decorator.Instance;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        return _inner.SignInAsync(context, scheme, principal, properties);
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        return _inner.SignOutAsync(context, scheme, properties);
    }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
    {
        return _inner.AuthenticateAsync(context, scheme);
    }

    public async Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        await _inner.ChallengeAsync(context, scheme, properties);

        if (context.Response.StatusCode != 302)
        {
            return;
        }

        var endpoint = context.GetEndpoint();

        var isBffEndpoint = endpoint?.Metadata.GetMetadata<IBffApiEndpoint>() != null;
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

        var isBffEndpoint = endpoint?.Metadata.GetMetadata<IBffApiEndpoint>() != null;
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

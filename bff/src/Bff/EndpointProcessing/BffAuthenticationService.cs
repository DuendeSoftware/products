// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff;

// this decorates the real authentication service to detect when
// Challenge of Forbid is being called for a BFF API endpoint
internal class BffAuthenticationService : IAuthenticationService
{
    private readonly IAuthenticationService _inner;
    private readonly ILogger<BffAuthenticationService> _logger;

    public BffAuthenticationService(
        Decorator<IAuthenticationService> decorator,
        ILogger<BffAuthenticationService> logger)
    {
        _inner = decorator.Instance;
        _logger = logger;
    }

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

        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            if (context.Response.StatusCode == 302)
            {
                var isBffEndpoint = endpoint.Metadata.GetMetadata<IBffApiEndpoint>() != null;
                if (isBffEndpoint)
                {
                    var requireResponseHandling = endpoint.Metadata.GetMetadata<IBffApiSkipResponseHandling>() == null;
                    if (requireResponseHandling)
                    {
                        _logger.LogDebug("Challenge was called for a BFF API endpoint, BFF response handling changing status code to 401.");

                        context.Response.StatusCode = 401;
                        context.Response.Headers.Remove("Location");
                        context.Response.Headers.Remove("Set-Cookie");
                    }
                }
            }
        }
    }

    public async Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        await _inner.ForbidAsync(context, scheme, properties);

        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            if (context.Response.StatusCode == 302)
            {
                var isBffEndpoint = endpoint.Metadata.GetMetadata<IBffApiEndpoint>() != null;
                if (isBffEndpoint)
                {
                    var requireResponseHandling = endpoint.Metadata.GetMetadata<IBffApiSkipResponseHandling>() == null;
                    if (requireResponseHandling)
                    {
                        _logger.LogDebug("Forbid was called for a BFF API endpoint, BFF response handling changing status code to 403.");

                        context.Response.StatusCode = 403;
                        context.Response.Headers.Remove("Location");
                        context.Response.Headers.Remove("Set-Cookie");
                    }
                }
            }
        }
    }
}

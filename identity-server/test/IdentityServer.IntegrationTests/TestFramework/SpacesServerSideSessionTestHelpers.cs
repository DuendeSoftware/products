// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.TestFramework;

internal static class SpacesServerSideSessionTestHelpers
{
    // The ticket store derives the issuer from the current request, so give it a minimal one.
    public static void UseRequestContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
    }

    public static AuthenticationTicket CreateTicket() => CreateTicket([]);

    public static AuthenticationTicket CreateTicket(IReadOnlyCollection<string> clientIds)
    {
        var identity = new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "alice")], "test", JwtClaimTypes.Name, JwtClaimTypes.Role);
        var properties = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        properties.SetSessionId("sid-1");
        if (clientIds.Count > 0)
        {
            properties.SetClientList(clientIds);
        }

        return new AuthenticationTicket(new ClaimsPrincipal(identity), properties, "idsrv");
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using IdentityServerHost.Configuration;
using IdentityServerHost.Models;

namespace IdentityServerHost;

internal static class IdentityServerExtensions
{
    internal static WebApplicationBuilder ConfigureIdentityServer(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityServer()
            .AddInMemoryIdentityResources(TestResources.IdentityResources)
            .AddInMemoryApiResources(TestResources.ApiResources)
            .AddInMemoryApiScopes(TestResources.ApiScopes)
            .AddInMemoryClients(TestClients.Get())
            .AddAspNetIdentity<ApplicationUser>();

        return builder;
    }
}

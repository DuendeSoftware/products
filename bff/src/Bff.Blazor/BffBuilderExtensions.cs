// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Blazor;

public static class BffBuilderExtensions
{
    public static BffBuilder AddBlazorServer(this BffBuilder builder)
    {
        builder.Services
            .AddOpenIdConnectAccessTokenManagement()
            .AddBlazorServerAccessTokenManagement<ServerSideTokenStore>()
            .AddTransient<IClaimsTransformation, AddServerManagementClaimsTransform>();

        return builder;
    }
}

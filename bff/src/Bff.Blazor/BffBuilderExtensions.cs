// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Blazor;

public static class BffBuilderExtensions
{
    public static BffBuilder AddBlazorServer(this BffBuilder builder, Action<BffBlazorServerOptions>? configureOptions = null)
    {
        builder.Services
            .AddOpenIdConnectAccessTokenManagement()
#pragma warning disable CS0618 // Type or member is obsolete
            .AddBlazorServerAccessTokenManagement<ServerSideTokenStore>()
            .AddSingleton<IClaimsTransformation, AddServerManagementClaimsTransform>()
            .AddScoped<AuthenticationStateProvider, BffServerAuthenticationStateProvider>(); ;
#pragma warning restore CS0618 // Type or member is obsolete


        if (configureOptions != null)
        {
            builder.Services.Configure(configureOptions);
        }
        return builder;
    }
}

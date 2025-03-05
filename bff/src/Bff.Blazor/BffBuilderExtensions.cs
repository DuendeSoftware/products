// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Blazor.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Blazor;

public static class BffBuilderExtensions
{
    /// <summary>
    /// Adds Blazor support to a BFF application. 
    /// </summary>
    /// <param name="builder">The bff blazor builder</param>
    /// <param name="configureOptions">Allows you to configure <see cref="BffBlazorServerOptions"/></param>
    /// <returns></returns>
    public static BffBuilder AddBlazorServer(this BffBuilder builder, Action<BffBlazorServerOptions>? configureOptions = null)
    {
        builder.Services
            .AddOpenIdConnectAccessTokenManagement()
            .AddBlazorServerAccessTokenManagement<ServerSideTokenStore>()
            .AddScoped<AuthenticationStateProvider, BffServerAuthenticationStateProvider>();

        if (configureOptions != null)
        {
            builder.Services.Configure(configureOptions);
        }

        return builder;
    }
}

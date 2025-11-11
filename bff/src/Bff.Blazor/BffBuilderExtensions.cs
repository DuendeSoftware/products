// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Builder;
using Duende.Bff.SessionManagement.SessionStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Blazor;

public static class
    BffBuilderExtensions
{
    public static T AddBlazorServer<T>(this T builder, Action<BffBlazorServerOptions>? configureOptions = null) where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddActivatedSingleton<ServerSideSessionChecker>();

        builder.Services
            .AddOpenIdConnectAccessTokenManagement()
            .AddBlazorServerAccessTokenManagement<ServerSideTokenStore>()
            .AddSingleton<IClaimsTransformation, AddServerManagementClaimsTransform>()
            .AddScoped<AuthenticationStateProvider, BffServerAuthenticationStateProvider>();


        if (configureOptions != null)
        {
            builder.Services.Configure(configureOptions);
        }

        return builder;
    }

    /// <summary>
    /// This class sole purpose is to ensure that server-side sessions are configured
    /// If not, it will throw an exception when the BFF starting up.
    /// </summary>
    internal class ServerSideSessionChecker
    {
        public ServerSideSessionChecker(IServiceProvider sp)
        {
            using var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateScope();
            
            var sessions = scope.ServiceProvider.GetService<IUserSessionStore>();

            if (sessions == null)
            {
                throw new InvalidOperationException(
                    "Server-side sessions are not configured. Please call bff.AddServerSideSessions() in your BFF configuration.");
            }
        }
    }
}

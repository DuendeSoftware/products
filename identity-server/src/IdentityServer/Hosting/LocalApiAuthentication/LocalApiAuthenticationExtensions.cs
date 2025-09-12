// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Hosting.LocalApiAuthentication;
using Microsoft.AspNetCore.Authentication;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for registering the local access token authentication handler
/// </summary>
public static class LocalApiAuthenticationExtensions
{
    /// <summary>
    /// Adds support for local APIs
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="transformationFunc">Function to transform the resulting principal</param>
    /// <returns></returns>
    public static IServiceCollection AddLocalApiAuthentication(this IServiceCollection services, Func<ClaimsPrincipal, Task<ClaimsPrincipal>>? transformationFunc = null)
    {
        _ = services.AddAuthentication()
            .AddLocalApi(options =>
            {
                options.ExpectedScope = IdentityServerConstants.LocalApi.ScopeName;

                if (transformationFunc != null)
                {
                    options.Events = new LocalApiAuthenticationEvents
                    {
                        OnClaimsTransformation = async e =>
                        {
                            e.Principal = await transformationFunc(e.Principal);
                        }
                    };
                }
            });

        _ = services.AddAuthorization(options =>
        {
            options.AddPolicy(IdentityServerConstants.LocalApi.PolicyName, policy =>
            {
                _ = policy.AddAuthenticationSchemes(IdentityServerConstants.LocalApi.AuthenticationScheme);
                _ = policy.RequireAuthenticatedUser();
            });
        });

        return services;
    }

    /// <summary>
    /// Registers the authentication handler for local APIs.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddLocalApi(this AuthenticationBuilder builder)
        => builder.AddLocalApi(IdentityServerConstants.LocalApi.AuthenticationScheme, _ => { });

    /// <summary>
    /// Registers the authentication handler for local APIs.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configureOptions">The configure options.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddLocalApi(this AuthenticationBuilder builder, Action<LocalApiAuthenticationOptions> configureOptions)
        => builder.AddLocalApi(IdentityServerConstants.LocalApi.AuthenticationScheme, configureOptions);

    /// <summary>
    /// Registers the authentication handler for local APIs.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <param name="configureOptions">The configure options.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddLocalApi(this AuthenticationBuilder builder, string authenticationScheme, Action<LocalApiAuthenticationOptions> configureOptions)
        => builder.AddLocalApi(authenticationScheme, displayName: null, configureOptions: configureOptions);

    /// <summary>
    /// Registers the authentication handler for local APIs.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="authenticationScheme">The authentication scheme.</param>
    /// <param name="displayName">The display name of this scheme.</param>
    /// <param name="configureOptions">The configure options.</param>
    /// <returns></returns>
    public static AuthenticationBuilder AddLocalApi(this AuthenticationBuilder builder, string authenticationScheme, string? displayName, Action<LocalApiAuthenticationOptions> configureOptions) => builder.AddScheme<LocalApiAuthenticationOptions, LocalApiAuthenticationHandler>(authenticationScheme, displayName, configureOptions);
}

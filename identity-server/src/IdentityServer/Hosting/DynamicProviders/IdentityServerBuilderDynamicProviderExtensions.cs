// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering custom dynamic identity providers.
/// </summary>
public static class IdentityServerBuilderDynamicProviderExtensions
{
    /// <summary>
    /// Registers a dynamic identity provider type with the given protocol type string,
    /// authentication handler, handler options, identity provider model, and configure options.
    /// </summary>
    /// <typeparam name="THandler">The authentication handler type (e.g., <c>WsFederationHandler</c>).</typeparam>
    /// <typeparam name="TProviderOptions">The authentication scheme options type (e.g., <c>WsFederationOptions</c>).</typeparam>
    /// <typeparam name="TProvider">The identity provider model type (e.g., <c>WsFedProvider</c>).</typeparam>
    /// <typeparam name="TConfigureOptions">
    /// The <see cref="ConfigureAuthenticationOptions{TAuthenticationOptions, TIdentityProvider}"/> implementation
    /// that maps from the identity provider model to the authentication handler options.
    /// </typeparam>
    /// <param name="builder">The IdentityServer builder.</param>
    /// <param name="type">The protocol type string (e.g., <c>"wsfed"</c>) used as the discriminator in the identity provider store.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddDynamicProvider<THandler, TProviderOptions, TProvider, TConfigureOptions>(
        this IIdentityServerBuilder builder,
        string type)
        where THandler : class, IAuthenticationRequestHandler
        where TProviderOptions : AuthenticationSchemeOptions, new()
        where TProvider : IdentityProvider, new()
        where TConfigureOptions : ConfigureAuthenticationOptions<TProviderOptions, TProvider>
    {
        builder.Services.Configure<IdentityServerOptions>(options =>
        {
            options.DynamicProviders.AddProviderType<THandler, TProviderOptions, TProvider>(type);
        });

        builder.Services.AddSingleton<IConfigureOptions<TProviderOptions>, TConfigureOptions>();
        builder.Services.TryAddTransient<THandler>();

        return builder;
    }
}

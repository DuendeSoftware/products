// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Add extension methods for configuring OIDC dynamic providers.
/// </summary>
public static class IdentityServerBuilderOidcExtensions
{
    /// <summary>
    /// Adds the OIDC dynamic provider feature.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddOidcDynamicProvider(this IIdentityServerBuilder builder)
    {
        builder.AddDynamicProvider<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider, OidcConfigureOptions>("oidc");

        // these are services from ASP.NET Core and are added manually since we're not using the
        // AddOpenIdConnect helper that we'd normally use statically on the AddAuthentication.
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>());

        return builder;
    }

    /// <summary>
    /// Adds the in memory OIDC provider store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="providers"></param>
    /// <returns></returns>
    public static IIdentityServerBuilder AddInMemoryOidcProviders(this IIdentityServerBuilder builder, IEnumerable<OidcProvider> providers) => builder.AddInMemoryIdentityProviders(providers);
}

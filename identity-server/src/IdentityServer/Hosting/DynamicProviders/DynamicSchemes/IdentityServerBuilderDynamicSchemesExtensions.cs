// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;

namespace Microsoft.Extensions.DependencyInjection;
/// <summary>
/// Add extension methods for configuring generic dynamic providers.
/// </summary>
public static class IdentityServerBuilderDynamicSchemesExtensions
{
    /// <summary>
    /// Adds the in memory identity provider store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="providers"></param>
    public static IIdentityServerBuilder AddInMemoryIdentityProviders(
        this IIdentityServerBuilder builder, IEnumerable<IdentityProvider> providers)
    {
        builder.Services.AddSingleton(providers);
        builder.AddIdentityProviderStore<InMemoryIdentityProviderStore>();

        return builder;
    }
}

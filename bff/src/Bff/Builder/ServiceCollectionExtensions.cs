// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Builder;

internal static class ServiceCollectionExtensions
{
    internal static void AddDelegatedToRootContainer<T>(this IServiceCollection appCollection) where T : class
    {
        appCollection.AddScoped<RootContainer.DelegatedScope>();
        appCollection.AddTransient<T>(sp =>
        {
            var delegatedScope = sp.GetRequiredService<RootContainer.DelegatedScope>();
            return delegatedScope.Resolve<T>();
        });
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Bff.Builder;

internal static class ServiceCollectionExtensions
{
    internal static void AddDelegatedToRootContainer<T>(this IServiceCollection appCollection) where T : class
    {
        appCollection.TryAddScoped<RootContainer.DelegatedScope>();
        appCollection.AddTransient<T>(sp =>
        {
            var delegatedScope = sp.GetRequiredService<RootContainer.DelegatedScope>();
            return delegatedScope.Resolve<T>();
        });
    }
}

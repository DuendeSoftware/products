// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Storage.Internal;

/// <summary>
/// Extension methods for configuring storage services.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class StorageBuilderExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures storage services using the provided builder callback.
        /// </summary>
        public IServiceCollection AddStorageInternal(Action<IStorageBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var builder = new StorageBuilder(services);
            services.TryAddSingleton<IStoreFactory, DefaultPoolStoreFactory>();
            configure(builder);
            return services;
        }
    }

    private sealed class StorageBuilder(IServiceCollection services) : IStorageBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}

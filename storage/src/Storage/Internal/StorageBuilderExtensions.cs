// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Internal;

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
            configure(builder);
            return services;
        }
    }

    private sealed class StorageBuilder(IServiceCollection services) : IStorageBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}

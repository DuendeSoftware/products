// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Storage.Internal;

internal static class StorageProviderSelectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Records that the specified provider has been selected for the builder's storage instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a provider has already been selected for the builder's storage instance.
        /// </exception>
        internal void MarkProviderSelected(string providerName)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrEmpty(providerName);

            builder.Services.GetOrAddProviderRegistry().SelectProvider(builder.StorageInstanceId, providerName);
        }
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Gets the <see cref="StorageProviderRegistry"/> registered in the service collection,
        /// registering a new one as a singleton if none exists yet.
        /// </summary>
        private StorageProviderRegistry GetOrAddProviderRegistry()
        {
            if (services
                    .FirstOrDefault(x => x.ImplementationInstance is StorageProviderRegistry)
                    ?.ImplementationInstance is not StorageProviderRegistry existing)
            {
                existing = new StorageProviderRegistry();
                services.TryAddSingleton(existing);
            }

            return existing;
        }
    }
}

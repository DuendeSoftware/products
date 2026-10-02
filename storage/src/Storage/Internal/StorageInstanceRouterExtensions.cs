// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Storage.Internal;

/// <summary>
/// Extension methods for obtaining the <see cref="IStorageInstanceRouter"/> registered in a
/// service collection.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class StorageInstanceRouterExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Gets the <see cref="IStorageInstanceRouter"/> registered in the service collection,
        /// registering a new one as a singleton if none exists yet.
        /// </summary>
        public IStorageInstanceRouter GetOrAddStorageInstanceRouter()
        {
            var existing = services
                    .FirstOrDefault(x => x.ImplementationInstance is StorageInstanceRouter)
                    ?.ImplementationInstance
                as StorageInstanceRouter;

            if (existing == null)
            {
                existing = new StorageInstanceRouter();
                services.TryAddSingleton<IStorageInstanceRouter>(existing);
            }

            return existing;
        }
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Spaces;
using Duende.Spaces.Internal;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer;

/// <summary>
/// Extension methods for adding Duende Spaces to IdentityServer.
/// </summary>
public static class IdentityServerSpacesBuilderExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds Spaces to IdentityServer, storing Spaces data in the default database
        /// (<see cref="StorageInstanceId.Default"/>).
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>AddSpaces(StorageInstanceId.Default, configure)</c>. See that overload for details.
        /// </remarks>
        /// <param name="configure">A delegate to configure <see cref="SpacesOptions"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when Spaces data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddSpaces(Action<SpacesOptions> configure) =>
            builder.AddSpaces(StorageInstanceId.Default, configure);

        /// <summary>
        /// Adds Spaces to IdentityServer, storing Spaces data in the database registered for
        /// <paramref name="storageInstanceId"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Register a database for <paramref name="storageInstanceId"/> with <c>AddStorage</c>. Otherwise the
        /// first Spaces read or write throws an <see cref="InvalidOperationException"/> because the instance has
        /// no database provider.
        /// </para>
        /// <para>
        /// Call <c>UseSpaceResolution()</c> early in the request pipeline so each request runs in its space.
        /// </para>
        /// </remarks>
        /// <param name="storageInstanceId">The storage instance that holds Spaces data.</param>
        /// <param name="configure">A delegate to configure <see cref="SpacesOptions"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when Spaces data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddSpaces(StorageInstanceId storageInstanceId, Action<SpacesOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            _ = builder.Services.Configure(configure);
            _ = builder.Services.AddSpacesInternal(storageInstanceId);

            return builder;
        }
    }
}

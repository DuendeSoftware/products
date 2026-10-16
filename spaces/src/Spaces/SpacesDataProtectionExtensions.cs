// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;

namespace Duende.Spaces;

/// <summary>
/// Extensions for opting out of space partitioning for data protection.
/// </summary>
public static class SpacesDataProtectionExtensions
{
    extension(IDataProtectionProvider provider)
    {
        /// <summary>
        /// Gets the unpartitioned provider behind a space-partitioned provider, or the receiver if it
        /// is not partitioned.
        /// </summary>
        /// <returns>The unpartitioned provider, or the receiver.</returns>
        /// <seealso cref="IUnpartitionedDataProtectionSource"/>
        public IDataProtectionProvider GetUnpartitioned()
        {
            ArgumentNullException.ThrowIfNull(provider);
            return provider is IUnpartitionedDataProtectionSource source ? source.GetUnpartitioned() : provider;
        }
    }

    extension(IDataProtector protector)
    {
        /// <summary>
        /// Gets the unpartitioned protector behind a partitioned protector, or the receiver if it is
        /// not partitioned.
        /// </summary>
        /// <returns>The unpartitioned protector, or the receiver.</returns>
        /// <exception cref="InvalidOperationException">The source returned something that is not an <see cref="IDataProtector"/>.</exception>
        /// <seealso cref="IUnpartitionedDataProtectionSource"/>
        public IDataProtector GetUnpartitioned()
        {
            ArgumentNullException.ThrowIfNull(protector);
            if (protector is not IUnpartitionedDataProtectionSource source)
            {
                return protector;
            }

            return source.GetUnpartitioned() as IDataProtector
                ?? throw new InvalidOperationException(
                    "The unpartitioned data protection source returned a provider that is not an IDataProtector for a protector receiver.");
        }
    }
}

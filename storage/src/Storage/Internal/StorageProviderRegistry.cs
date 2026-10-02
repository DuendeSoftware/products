// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Records which database provider each <see cref="StorageInstanceId"/> uses, so that a storage
/// instance can only be backed by one provider.
/// </summary>
internal sealed class StorageProviderRegistry
{
    private readonly Dictionary<StorageInstanceId, string> _providers = new();

    /// <summary>
    /// Records that <paramref name="storageInstanceId"/> uses the provider named <paramref name="providerName"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a provider has already been selected for <paramref name="storageInstanceId"/>.
    /// </exception>
    public void SelectProvider(StorageInstanceId storageInstanceId, string providerName)
    {
        if (!_providers.TryAdd(storageInstanceId, providerName))
        {
            throw new InvalidOperationException(
                $"Storage instance '{storageInstanceId}' is already configured to use the {_providers[storageInstanceId]} provider, " +
                $"so it cannot also use the {providerName} provider. Configure exactly one provider per storage " +
                "instance, or register the second provider on a different storage instance.");
        }
    }
}

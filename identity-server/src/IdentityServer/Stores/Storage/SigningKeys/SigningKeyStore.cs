// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.Storage;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.SigningKeys;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SigningKeyStore(
    KeyRepository repository,
    ILogger<SigningKeyStore> logger) : ISigningKeyStore
{
    private const string Use = "signing";

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SigningKeyStore.LoadKeys");

        return await repository.LoadByUseAsync(Use, ct);
    }

    /// <inheritdoc/>
    public async Task StoreKeyAsync(SerializedKey key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SigningKeyStore.StoreKey");

        var result = await repository.CreateAsync(UuidV7.New(), key, Use, ct);

        if (result != CreateResult.Success)
        {
            logger.LogError("Failed to store signing key {KeyId}: {Result}", key.Id, result);
            throw new InvalidOperationException($"Could not store signing key '{key.Id}': {result}");
        }
    }

    /// <inheritdoc/>
    public async Task DeleteKeyAsync(string id, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SigningKeyStore.DeleteKey");

        await repository.DeleteByIdAsync(id, ct);
    }
}

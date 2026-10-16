// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

/// <summary>
/// Verifies that the Duende.Storage-backed <see cref="ISigningKeyStore"/> registered by
/// <c>AddOperationalStorage()</c> stores its data in the operational category.
/// </summary>
public sealed class StorageSigningKeyStoreCategoryTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Signing_key_store_uses_operational_storage_category()
    {
        await using var provider = await StorageContractTestServices.CreateAsync(
            $"StorageSigningKeyCategory_{Guid.NewGuid():N}", b => b.AddOperationalStorage());
        var store = provider.GetRequiredService<ISigningKeyStore>();
        var key = new SerializedKey
        {
            Id = $"key_{Guid.NewGuid():N}",
            Version = 1,
            Created = DateTime.UtcNow,
            Algorithm = "RS256",
            DataProtected = true,
            Data = $"serialized_key_data_{Guid.NewGuid():N}"
        };

        await store.StoreKeyAsync(key, _ct);
        var keys = await store.LoadKeysAsync(_ct);
        await store.DeleteKeyAsync(key.Id, _ct);

        keys.ShouldContain(k => k.Id == key.Id);
        DataCategoryNameRecorder.UsedCategoriesShouldBe(provider, DataCategoryName.Operational);
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

/// <summary>
/// Verifies that the Duende.Storage-backed <see cref="ICorsPolicyService"/> registered by
/// <c>AddConfigurationStorage()</c> reads clients from the configuration category.
/// </summary>
public sealed class StorageCorsPolicyServiceCategoryTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cors_policy_service_uses_configuration_storage_category()
    {
        await using var provider = await StorageContractTestServices.CreateAsync(
            $"StorageCorsCategory_{Guid.NewGuid():N}", b => b.AddConfigurationStorage());
        var corsPolicyService = provider.GetRequiredService<ICorsPolicyService>();

        var allowed = await corsPolicyService.IsOriginAllowedAsync("https://unknown.example.com", _ct);

        allowed.ShouldBeFalse();
        DataCategoryNameRecorder.UsedCategoriesShouldBe(provider, DataCategoryName.Configuration);
    }
}

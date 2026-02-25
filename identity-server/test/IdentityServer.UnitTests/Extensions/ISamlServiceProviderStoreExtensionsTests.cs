// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Runtime.CompilerServices;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Extensions;

public class ISamlServiceProviderStoreExtensionsTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindEnabledSamlServiceProviderByEntityIdAsync_WhenSpIsEnabled_ShouldReturnSp()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com", Enabled = true };
        var store = new StubSamlServiceProviderStore(sp);

        var result = await store.FindEnabledSamlServiceProviderByEntityIdAsync(sp.EntityId, _ct);

        result.ShouldNotBeNull();
        result.EntityId.ShouldBe(sp.EntityId);
    }

    [Fact]
    public async Task FindEnabledSamlServiceProviderByEntityIdAsync_WhenSpIsDisabled_ShouldReturnNull()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com", Enabled = false };
        var store = new StubSamlServiceProviderStore(sp);

        var result = await store.FindEnabledSamlServiceProviderByEntityIdAsync(sp.EntityId, _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindEnabledSamlServiceProviderByEntityIdAsync_WhenSpNotFound_ShouldReturnNull()
    {
        var store = new StubSamlServiceProviderStore(null);

        var result = await store.FindEnabledSamlServiceProviderByEntityIdAsync("https://notfound.example.com", _ct);

        result.ShouldBeNull();
    }

    private class StubSamlServiceProviderStore(SamlServiceProvider? sp) : ISamlServiceProviderStore
    {
        public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct _) => Task.FromResult(sp);

        public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct _)
        {
            if (sp != null)
            {
                yield return sp;
            }
        }
    }
}

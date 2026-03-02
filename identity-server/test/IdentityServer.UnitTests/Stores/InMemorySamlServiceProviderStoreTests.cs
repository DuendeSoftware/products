// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Stores;

public class InMemorySamlServiceProviderStoreTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void InMemorySamlServiceProviderStore_should_throw_if_contains_duplicate_entity_ids()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://sp1.example.com" },
            new() { EntityId = "https://sp1.example.com" },
            new() { EntityId = "https://sp3.example.com" }
        };

        Action act = () => new InMemorySamlServiceProviderStore(sps);
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void InMemorySamlServiceProviderStore_should_not_throw_if_no_duplicate_entity_ids()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://sp1.example.com" },
            new() { EntityId = "https://sp2.example.com" },
            new() { EntityId = "https://sp3.example.com" }
        };

        new InMemorySamlServiceProviderStore(sps);
    }

    [Fact]
    public async Task FindByEntityIdAsync_should_return_sp_when_found()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://sp1.example.com", Enabled = true },
            new() { EntityId = "https://sp2.example.com", Enabled = true }
        };
        var store = new InMemorySamlServiceProviderStore(sps);

        var result = await store.FindByEntityIdAsync("https://sp1.example.com", _ct);

        result.ShouldNotBeNull();
        result.EntityId.ShouldBe("https://sp1.example.com");
    }

    [Fact]
    public async Task FindByEntityIdAsync_should_return_null_when_not_found()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://sp1.example.com", Enabled = true }
        };
        var store = new InMemorySamlServiceProviderStore(sps);

        var result = await store.FindByEntityIdAsync("https://notfound.example.com", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByEntityIdAsync_should_return_null_when_sp_is_disabled()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://sp1.example.com", Enabled = false }
        };
        var store = new InMemorySamlServiceProviderStore(sps);

        var result = await store.FindByEntityIdAsync("https://sp1.example.com", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_should_return_all_sps()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://sp1.example.com" },
            new() { EntityId = "https://sp2.example.com" },
            new() { EntityId = "https://sp3.example.com" }
        };
        var store = new InMemorySamlServiceProviderStore(sps);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain(s => s.EntityId == "https://sp1.example.com");
        result.ShouldContain(s => s.EntityId == "https://sp2.example.com");
        result.ShouldContain(s => s.EntityId == "https://sp3.example.com");
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_should_return_empty_when_no_sps()
    {
        var store = new InMemorySamlServiceProviderStore([]);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}

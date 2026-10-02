// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Hosting.DynamicProviders;

public class DynamicAuthenticationSchemeCacheTests
{
    private const string Scheme = "my-oidc";

    private static DynamicAuthenticationScheme CreateScheme(string clientMarker)
    {
        var idp = new IdentityProvider("oidc") { Scheme = Scheme, DisplayName = clientMarker };
        return new DynamicAuthenticationScheme(idp, typeof(MockAuthenticationHandler));
    }

    // Resolves the real, DI-registered ISpaceContextAccessor and a DynamicAuthenticationSchemeCache
    // the way production does: AddDynamicProvidersCore registers the cache scoped, wired to the
    // accessor, so one DI scope stands in for one request.
    private static (ISpaceContextAccessor Accessor, DynamicAuthenticationSchemeCache Cache) CreateCacheWithAccessor()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddIdentityServer();
        services.AddSpaces();
        var provider = services.BuildServiceProvider();

        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var cache = provider.CreateScope().ServiceProvider.GetRequiredService<DynamicAuthenticationSchemeCache>();
        return (accessor, cache);
    }

    [Fact]
    public void Get_does_not_return_an_entry_added_in_a_different_configured_space()
    {
        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();
        var (accessor, cache) = CreateCacheWithAccessor();

        using (accessor.SetSpace(spaceA))
        {
            cache.Add(Scheme, CreateScheme("space-a"));
        }

        using (accessor.SetSpace(spaceB))
        {
            cache.Get(Scheme).ShouldBeNull();
        }
    }

    [Fact]
    public void Get_with_no_space_does_not_return_an_entry_added_in_a_configured_space()
    {
        var spaceA = SpaceId.New();
        var (accessor, cache) = CreateCacheWithAccessor();

        using (accessor.SetSpace(spaceA))
        {
            cache.Add(Scheme, CreateScheme("space-a"));
        }

        cache.Get(Scheme).ShouldBeNull();
    }

    [Fact]
    public void Get_with_explicitly_configured_default_space_returns_an_entry_added_unconfigured()
    {
        // An unconfigured space is treated as the Default space, so both resolve to the same key.
        var (accessor, cache) = CreateCacheWithAccessor();
        cache.Add(Scheme, CreateScheme("unconfigured"));

        using (accessor.SetSpace(SpaceId.Default))
        {
            cache.GetIdentityProvider<IdentityProvider>(Scheme)!.DisplayName.ShouldBe("unconfigured");
        }
    }

    [Fact]
    public void Get_in_the_same_configured_space_returns_the_entry_that_was_added()
    {
        var spaceA = SpaceId.New();
        var (accessor, cache) = CreateCacheWithAccessor();
        using var scope = accessor.SetSpace(spaceA);

        cache.Add(Scheme, CreateScheme("space-a"));

        cache.Get(Scheme).ShouldNotBeNull();
        cache.GetIdentityProvider<IdentityProvider>(Scheme)!.DisplayName.ShouldBe("space-a");
    }

    [Fact]
    public void switching_space_mid_request_must_not_return_the_previous_spaces_entry()
    {
        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();
        var (accessor, cache) = CreateCacheWithAccessor();
        using var scopeA = accessor.SetSpace(spaceA);

        cache.Add(Scheme, CreateScheme("space-a"));

        cache.Get(Scheme).ShouldNotBeNull();

        using (accessor.SetSpace(spaceB))
        {
            cache.Get(Scheme).ShouldBeNull();

            cache.Add(Scheme, CreateScheme("space-b"));
            cache.GetIdentityProvider<IdentityProvider>(Scheme)!.DisplayName.ShouldBe("space-b");
        }

        cache.GetIdentityProvider<IdentityProvider>(Scheme)!.DisplayName.ShouldBe("space-a");
    }
}

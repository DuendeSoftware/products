// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Hosting.DynamicProviders.Store;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UnitTests.Hosting.DynamicProviders;

public class SpaceAwareDynamicOptionsMonitorCacheTests
{
    // Builds the real, DI-resolved ISpaceContextAccessor. AddIdentityServer registers the license
    // services Spaces depends on; with no license key configured they run in trial mode, so
    // SetSpace does not throw. SpaceAwareDynamicOptionsMonitorCache<TestOptions> cannot itself be
    // resolved from DI (TestOptions is a test-only options type no provider registers), so only the
    // accessor comes from DI and the cache under test is constructed directly with it.
    private static ISpaceContextAccessor CreateAccessor()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddIdentityServer();
        services.AddSpaces();
        return services.BuildServiceProvider().GetRequiredService<ISpaceContextAccessor>();
    }

    private static (HttpContextAccessor HttpContextAccessor, DynamicAuthenticationSchemeCache SchemeCache) BuildHttpContext()
    {
        var schemeCache = new DynamicAuthenticationSchemeCache();
        var services = new ServiceCollection();
        services.AddSingleton(schemeCache);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        return (new HttpContextAccessor { HttpContext = httpContext }, schemeCache);
    }

    // Forces a subsequent lookup for `name` to be treated as static, by swapping to an empty
    // DynamicAuthenticationSchemeCache.
    private static void SwapToNonDynamicRequest(HttpContextAccessor httpContextAccessor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DynamicAuthenticationSchemeCache());
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    private static void MarkDynamic(DynamicAuthenticationSchemeCache schemeCache, string name)
    {
        var idp = new IdentityProvider("oidc") { Scheme = name };
        schemeCache.Add(name, new DynamicAuthenticationScheme(idp, typeof(TestHandler)));
    }

    // Simulates moving to a new request that resolves `name` as a dynamic scheme, as
    // DynamicAuthenticationSchemeProvider.GetSchemeAsync does before a handler reads options.
    private static void StartNewRequestWithResolvedScheme(HttpContextAccessor httpContextAccessor, string name)
    {
        var schemeCache = new DynamicAuthenticationSchemeCache();
        MarkDynamic(schemeCache, name);
        var services = new ServiceCollection();
        services.AddSingleton(schemeCache);
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    private sealed class TestHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;
        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class TestOptions
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void GetOrAdd_with_null_accessor_routes_dynamic_name_to_default_space_in_dynamic_store()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(null, httpContextAccessor);

        var options = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "a" });

        options.Value.ShouldBe("a");
        cache.TryAdd("my-oidc", new TestOptions { Value = "b" }).ShouldBeFalse();

        SwapToNonDynamicRequest((HttpContextAccessor)httpContextAccessor);
        cache.TryAdd("my-oidc", new TestOptions { Value = "unqualified" }).ShouldBeTrue();
    }

    [Fact]
    public void GetOrAdd_with_unconfigured_accessor_routes_dynamic_name_to_default_space_in_dynamic_store()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);
        cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "a" });

        cache.TryAdd("my-oidc", new TestOptions { Value = "b" }).ShouldBeFalse();

        SwapToNonDynamicRequest((HttpContextAccessor)httpContextAccessor);
        cache.TryAdd("my-oidc", new TestOptions { Value = "unqualified" }).ShouldBeTrue();
    }

    [Fact]
    public void GetOrAdd_with_configured_default_space_routes_dynamic_name_to_configured_dynamic_store_under_qualified_key()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        using var scope = accessor.SetSpace(SpaceId.Default);

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);
        cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "configured" });

        SwapToNonDynamicRequest((HttpContextAccessor)httpContextAccessor);
        cache.TryAdd("my-oidc", new TestOptions { Value = "unqualified" }).ShouldBeTrue();

        ((ISpaceAwareDynamicOptionsCache)cache).TryRemoveDynamic("my-oidc").ShouldBeTrue();
    }

    [Fact]
    public void GetOrAdd_with_configured_non_default_space_routes_dynamic_name_to_configured_dynamic_store()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        using var scope = accessor.SetSpace(SpaceId.New());

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);
        cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "configured" });

        SwapToNonDynamicRequest((HttpContextAccessor)httpContextAccessor);
        cache.TryAdd("my-oidc", new TestOptions { Value = "unqualified" }).ShouldBeTrue();
        ((ISpaceAwareDynamicOptionsCache)cache).TryRemoveDynamic("my-oidc").ShouldBeTrue();
    }

    [Fact]
    public void GetOrAdd_called_twice_with_same_bare_name_in_same_space_returns_same_cached_instance()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        using var scope = accessor.SetSpace(SpaceId.New());

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);

        var first = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "first" });
        var second = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "second" });

        second.ShouldBeSameAs(first);
        second.Value.ShouldBe("first");
    }

    [Fact]
    public void GetOrAdd_with_unconfigured_space_shares_the_dynamic_entry_with_configured_default_space()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);
        var unconfigured = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "unconfigured" });

        using (accessor.SetSpace(SpaceId.Default))
        {
            var configuredDefault = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "configured-default" });

            configuredDefault.ShouldBeSameAs(unconfigured);
        }
    }

    [Fact]
    public void IsDynamic_gating_routes_name_present_in_scheme_cache_with_configured_space_to_configured_dynamic_store()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        using var scope = accessor.SetSpace(SpaceId.New());

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);
        cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "configured" });

        // Present and configured -> routed to configured-dynamic, leaving the bare-named
        // unqualified slot free.
        SwapToNonDynamicRequest((HttpContextAccessor)httpContextAccessor);
        cache.TryAdd("my-oidc", new TestOptions()).ShouldBeTrue();
    }

    [Fact]
    public void IsDynamic_gating_routes_absent_static_name_to_unqualified_store_even_with_configured_space()
    {
        var (httpContextAccessor, _) = BuildHttpContext();
        var accessor = CreateAccessor();
        using var scope = accessor.SetSpace(SpaceId.New());

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);
        cache.GetOrAdd("static-scheme", () => new TestOptions { Value = "static" });

        cache.TryAdd("static-scheme", new TestOptions()).ShouldBeFalse();
    }

    [Fact]
    public void collision_between_bare_static_name_equal_to_composed_key_and_configured_dynamic_entry_is_isolated()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var spaceId = SpaceId.New();
        var composedName = $"{spaceId.Value}_foo";
        MarkDynamic(schemeCache, "foo");
        var accessor = CreateAccessor();
        using var scope = accessor.SetSpace(spaceId);

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);

        cache.TryAdd(composedName, new TestOptions { Value = "static-collision" }).ShouldBeTrue();

        var dynamicOptions = cache.GetOrAdd("foo", () => new TestOptions { Value = "dynamic" });
        dynamicOptions.Value.ShouldBe("dynamic");

        cache.TryAdd(composedName, new TestOptions { Value = "overwrite-attempt" }).ShouldBeFalse();

        ((ISpaceAwareDynamicOptionsCache)cache).TryRemoveDynamic("foo").ShouldBeTrue();

        cache.TryAdd(composedName, new TestOptions { Value = "still-static" }).ShouldBeFalse();
    }

    [Fact]
    public void unconfigured_dynamic_name_crafted_to_equal_configured_entrys_composed_key_is_isolated()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var spaceId = SpaceId.New();
        var composedName = $"{spaceId.Value}_foo";

        // Mark the composedName string itself as dynamic, but request it with no configured space.
        MarkDynamic(schemeCache, composedName);
        MarkDynamic(schemeCache, "foo");

        var accessor = CreateAccessor();
        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);

        cache.GetOrAdd(composedName, () => new TestOptions { Value = "unconfigured-dynamic" });

        using (accessor.SetSpace(spaceId))
        {
            var configuredOptions = cache.GetOrAdd("foo", () => new TestOptions { Value = "configured-dynamic" });

            configuredOptions.Value.ShouldBe("configured-dynamic");
        }

        cache.GetOrAdd(composedName, () => new TestOptions { Value = "should-not-be-used" })
            .Value.ShouldBe("unconfigured-dynamic");
    }

    [Fact]
    public void GetOrAdd_without_scheme_resolution_in_another_space_does_not_return_first_spaces_configured_options()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);

        using (accessor.SetSpace(SpaceId.New()))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "client-a" });
        }

        // A different space's request that never resolves the scheme: IsDynamic gating sees no
        // entry for "my-oidc" in its own request-scoped scheme cache, so this routes to the
        // unqualified store, not space A's configured-dynamic entry.
        using (accessor.SetSpace(SpaceId.New()))
        {
            SwapToNonDynamicRequest((HttpContextAccessor)httpContextAccessor);

            var observed = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "unconfigured-default" });

            observed.Value.ShouldNotBe("client-a");
            observed.Value.ShouldBe("unconfigured-default");
        }
    }

    [Fact]
    public void GetOrAdd_resolves_each_spaces_own_options_after_scheme_resolution_in_that_space()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var spaceAId = SpaceId.New();
        var spaceBId = SpaceId.New();
        var accessor = CreateAccessor();

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);

        TestOptions spaceAOptions;
        TestOptions spaceBOptions;
        TestOptions spaceAOptionsAgain;

        using (accessor.SetSpace(spaceAId))
        {
            StartNewRequestWithResolvedScheme(httpContextAccessor, "my-oidc");
            spaceAOptions = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "client-a" });
        }

        using (accessor.SetSpace(spaceBId))
        {
            StartNewRequestWithResolvedScheme(httpContextAccessor, "my-oidc");
            spaceBOptions = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "client-b" });
        }

        spaceAOptions.Value.ShouldBe("client-a");
        spaceBOptions.Value.ShouldBe("client-b");

        using (accessor.SetSpace(spaceAId))
        {
            StartNewRequestWithResolvedScheme(httpContextAccessor, "my-oidc");
            spaceAOptionsAgain = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "should-not-be-used" });
        }

        spaceAOptionsAgain.Value.ShouldBe("client-a",
            "space A's entry must be unaffected by space B's later resolution of the same scheme name");
    }

    [Fact]
    public void TryRemove_outside_a_request_evicts_the_bare_name_in_every_configured_space()
    {
        var httpContextAccessor = new HttpContextAccessor();
        var spaceAId = SpaceId.New();
        var spaceBId = SpaceId.New();
        var accessor = CreateAccessor();

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);

        using (accessor.SetSpace(spaceAId))
        {
            StartNewRequestWithResolvedScheme(httpContextAccessor, "my-oidc");
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "client-a" });
        }

        using (accessor.SetSpace(spaceBId))
        {
            StartNewRequestWithResolvedScheme(httpContextAccessor, "my-oidc");
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "client-b" });
        }

        // Simulates OptionsMonitor<T>.InvokeChanged reacting to a framework config reload: no
        // HttpContext, so the scheme cannot be resolved as dynamic for the current call.
        httpContextAccessor.HttpContext = null;
        ((IOptionsMonitorCache<TestOptions>)cache).TryRemove("my-oidc").ShouldBeTrue();

        TestOptions spaceAOptionsAfterRemove;
        TestOptions spaceBOptionsAfterRemove;

        using (accessor.SetSpace(spaceAId))
        {
            StartNewRequestWithResolvedScheme(httpContextAccessor, "my-oidc");
            spaceAOptionsAfterRemove = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "client-a-reloaded" });
        }

        using (accessor.SetSpace(spaceBId))
        {
            StartNewRequestWithResolvedScheme(httpContextAccessor, "my-oidc");
            spaceBOptionsAfterRemove = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "client-b-reloaded" });
        }

        spaceAOptionsAfterRemove.Value.ShouldBe("client-a-reloaded",
            "space A's entry must have been evicted so the reload factory runs again");
        spaceBOptionsAfterRemove.Value.ShouldBe("client-b-reloaded",
            "space B's entry must have been evicted so the reload factory runs again");
    }

    [Fact]
    public void Clear_empties_both_backing_stores()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        using var scope = accessor.SetSpace(SpaceId.New());

        var cache = new SpaceAwareDynamicOptionsMonitorCache<TestOptions>(accessor, httpContextAccessor);

        cache.TryAdd("static-scheme", new TestOptions { Value = "static" });
        cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "dynamic" });

        cache.Clear();

        cache.TryAdd("static-scheme", new TestOptions()).ShouldBeTrue();
        cache.TryAdd("my-oidc", new TestOptions()).ShouldBeTrue();
    }
}

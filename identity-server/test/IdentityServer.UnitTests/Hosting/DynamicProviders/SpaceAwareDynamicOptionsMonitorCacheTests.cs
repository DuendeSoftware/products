// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer;
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
        services.AddIdentityServer().AddSpaces(_ => { });
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

    private static SpaceAwareDynamicOptionsMonitorCache<TestOptions> CreateCache(
        ISpaceContextAccessor? accessor,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider? timeProvider = null,
        TimeSpan? idleTimeout = null) =>
        new(accessor, httpContextAccessor, timeProvider ?? TimeProvider.System, idleTimeout ?? Timeout.InfiniteTimeSpan);

    [Fact]
    public void GetOrAdd_with_null_accessor_routes_dynamic_name_to_default_space_in_dynamic_store()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");

        var cache = CreateCache(null, httpContextAccessor);

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

        var cache = CreateCache(accessor, httpContextAccessor);
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

        var cache = CreateCache(accessor, httpContextAccessor);
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

        var cache = CreateCache(accessor, httpContextAccessor);
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

        var cache = CreateCache(accessor, httpContextAccessor);

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

        var cache = CreateCache(accessor, httpContextAccessor);
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

        var cache = CreateCache(accessor, httpContextAccessor);
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

        var cache = CreateCache(accessor, httpContextAccessor);
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

        var cache = CreateCache(accessor, httpContextAccessor);

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
        var cache = CreateCache(accessor, httpContextAccessor);

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

        var cache = CreateCache(accessor, httpContextAccessor);

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

        var cache = CreateCache(accessor, httpContextAccessor);

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

        var cache = CreateCache(accessor, httpContextAccessor);

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

        var cache = CreateCache(accessor, httpContextAccessor);

        cache.TryAdd("static-scheme", new TestOptions { Value = "static" });
        cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "dynamic" });

        cache.Clear();

        cache.TryAdd("static-scheme", new TestOptions()).ShouldBeTrue();
        cache.TryAdd("my-oidc", new TestOptions()).ShouldBeTrue();
    }

    [Fact]
    public void idle_dynamic_entry_is_swept_after_idle_timeout_elapses()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var spaceId = SpaceId.New();

        var cache = CreateCache(accessor, httpContextAccessor, time, idleTimeout);

        using (accessor.SetSpace(spaceId))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "first" });
        }

        // Advance well past the idle timeout and trigger a sweep via another GetOrAdd call in a
        // different space (the sweep interval, timeout/4, has elapsed too).
        time.Advance(idleTimeout + idleTimeout);
        using (accessor.SetSpace(SpaceId.New()))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "trigger-sweep" });
        }

        using (accessor.SetSpace(spaceId))
        {
            var rebuilt = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "rebuilt" });
            rebuilt.Value.ShouldBe("rebuilt", "the idle entry must have been swept and rebuilt from the factory");

            var again = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "should-not-be-used" });
            again.ShouldBeSameAs(rebuilt, "the dictionary must hold the live replacement entry, not keep rebuilding");
        }
    }

    [Fact]
    public void entry_touched_within_idle_timeout_survives_a_sweep()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var spaceId = SpaceId.New();

        var cache = CreateCache(accessor, httpContextAccessor, time, idleTimeout);

        TestOptions first;
        using (accessor.SetSpace(spaceId))
        {
            first = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "first" });
        }

        // Advance by less than the idle timeout, touching the entry each time, then trigger
        // enough sweeps (sweep interval is idleTimeout/4) that a naive "last sweep" check alone
        // would not protect it; only the touches do.
        for (var i = 0; i < 6; i++)
        {
            time.Advance(TimeSpan.FromMinutes(4));
            using (accessor.SetSpace(spaceId))
            {
                var observed = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "should-not-be-used" });
                observed.ShouldBeSameAs(first);
            }
        }
    }

    [Fact]
    public void idle_entries_across_multiple_spaces_are_all_swept()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();

        var cache = CreateCache(accessor, httpContextAccessor, time, idleTimeout);

        using (accessor.SetSpace(spaceA))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "a" });
        }

        using (accessor.SetSpace(spaceB))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "b" });
        }

        time.Advance(idleTimeout + idleTimeout);

        // Any dynamic call triggers the opportunistic sweep across all spaces.
        var spaceC = SpaceId.New();
        using (accessor.SetSpace(spaceC))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "c" });
        }

        using (accessor.SetSpace(spaceA))
        {
            var rebuilt = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "a-rebuilt" });
            rebuilt.Value.ShouldBe("a-rebuilt", "space A's idle entry must have been swept and rebuilt");
        }

        using (accessor.SetSpace(spaceB))
        {
            var rebuilt = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "b-rebuilt" });
            rebuilt.Value.ShouldBe("b-rebuilt", "space B's idle entry must have been swept and rebuilt");
        }
    }

    [Fact]
    public void entry_touched_between_sweeps_idle_check_and_eviction_cas_survives_the_sweep()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var spaceId = SpaceId.New();

        var cache = CreateCache(accessor, httpContextAccessor, time, idleTimeout);

        TestOptions first;
        using (accessor.SetSpace(spaceId))
        {
            first = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "first" });
        }

        time.Advance(idleTimeout + idleTimeout);

        // Forces the interleaving the fix closes: sweep has already decided the entry is idle
        // (read its timestamp) but has not yet CAS'd it to evicted. A request touches it in that
        // window, which must make the CAS fail so sweep leaves the entry alone.
        cache.OnEntryDecidedIdle = entry => entry.Touch(time);

        using (accessor.SetSpace(spaceId))
        {
            var observed = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "should-not-be-used" });
            observed.ShouldBeSameAs(first, "the entry touched during the sweep's race window must survive");
        }
    }

    [Fact]
    public void infinite_idle_timeout_disables_expiry()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var time = new FakeTimeProvider();
        var spaceId = SpaceId.New();

        var cache = CreateCache(accessor, httpContextAccessor, time, Timeout.InfiniteTimeSpan);

        TestOptions first;
        using (accessor.SetSpace(spaceId))
        {
            first = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "first" });
        }

        time.Advance(TimeSpan.FromDays(365));

        using (accessor.SetSpace(spaceId))
        {
            var observed = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "should-not-be-used" });
            observed.ShouldBeSameAs(first);
        }
    }

    [Fact]
    public void static_entry_in_unqualified_store_survives_a_sweep_past_the_idle_timeout()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);

        var cache = CreateCache(accessor, httpContextAccessor, time, idleTimeout);

        var first = cache.GetOrAdd("static-scheme", () => new TestOptions { Value = "first" });

        // Advance past the idle timeout and trigger the opportunistic sweep via a dynamic call;
        // the sweep only ever walks the dynamic store, so the static entry in the unqualified
        // store must be unaffected.
        time.Advance(idleTimeout + idleTimeout);
        using (accessor.SetSpace(SpaceId.New()))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "trigger-sweep" });
        }

        var observed = cache.GetOrAdd("static-scheme", () => new TestOptions { Value = "should-not-be-used" });
        observed.ShouldBeSameAs(first, "a static entry is never idle-swept; only the dynamic store is");
    }

    [Fact]
    public void sweep_does_not_run_again_within_a_quarter_of_the_idle_timeout_after_the_previous_sweep()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var spaceE = SpaceId.New();

        var cache = CreateCache(accessor, httpContextAccessor, time, idleTimeout);

        using (accessor.SetSpace(spaceE))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "e" }); // t = 0
        }

        // t = 9.9min: triggers a sweep (sweep interval, timeout/4 = 2.5min, has elapsed since
        // t=0). E is not yet idle (9.9min < 10min), so it survives, and this call becomes the
        // "last sweep" timestamp.
        time.Advance(TimeSpan.FromMinutes(9.9));
        using (accessor.SetSpace(SpaceId.New()))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "trigger-1" });
        }
        cache.ContainsDynamic(spaceE, "my-oidc").ShouldBeTrue();

        // t = 10.1min: E is now idle (10.1min >= 10min), but only 0.2min have passed since the
        // last sweep (< 2.5min sweep interval). The throttle must suppress this sweep entirely,
        // so E must survive even though it would be evicted if a sweep ran.
        time.Advance(TimeSpan.FromMinutes(0.2));
        using (accessor.SetSpace(SpaceId.New()))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "trigger-2" });
        }
        cache.ContainsDynamic(spaceE, "my-oidc").ShouldBeTrue(
            "the throttle must suppress a sweep within timeout/4 of the previous sweep");

        // t = 12.5min: 2.6min have now passed since the last sweep (>= 2.5min), so a sweep runs
        // again, and E (now 12.5min idle) must finally be removed.
        time.Advance(TimeSpan.FromMinutes(2.4));
        using (accessor.SetSpace(SpaceId.New()))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "trigger-3" });
        }
        cache.ContainsDynamic(spaceE, "my-oidc").ShouldBeFalse();
    }

    [Fact]
    public void caller_that_obtains_an_entry_already_marked_evicted_gets_a_fresh_working_entry_held_in_the_dictionary()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var accessor = CreateAccessor();
        var spaceId = SpaceId.New();

        var cache = CreateCache(accessor, httpContextAccessor);

        using (accessor.SetSpace(spaceId))
        {
            cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "first" });
        }

        // Mark the entry evicted in place, without going through the sweep (which would also
        // remove it from the dictionary), so GetOrAdd observes a dictionary entry whose Touch
        // fails, independent of any sweep timing.
        var entry = cache.GetEntryForTest(spaceId, "my-oidc");
        entry.ShouldNotBeNull();
        entry.TryMarkEvicted(entry.LastUsedTicksRaw).ShouldBeTrue();

        using (accessor.SetSpace(spaceId))
        {
            var rebuilt = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "rebuilt" });
            rebuilt.Value.ShouldBe("rebuilt", "an evicted entry must not be handed out: a fresh one must be built");

            var again = cache.GetOrAdd("my-oidc", () => new TestOptions { Value = "should-not-be-used" });
            again.ShouldBeSameAs(rebuilt, "the dictionary must hold the live replacement entry, not keep rebuilding");
        }
    }
}

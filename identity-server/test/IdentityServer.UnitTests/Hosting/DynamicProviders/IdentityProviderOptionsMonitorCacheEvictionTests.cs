// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UnitTests.Hosting.DynamicProviders;

public class IdentityProviderOptionsMonitorCacheEvictionTests
{
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

    private static void MarkDynamic(DynamicAuthenticationSchemeCache schemeCache, string name)
    {
        var idp = new IdentityProvider("oidc") { Scheme = name };
        schemeCache.Add(name, new DynamicAuthenticationScheme(idp, typeof(TestHandler)));
    }

    private sealed class TestHandler : Microsoft.AspNetCore.Authentication.IAuthenticationHandler
    {
        public Task InitializeAsync(Microsoft.AspNetCore.Authentication.AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;
        public Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> AuthenticateAsync() => Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        public Task ChallengeAsync(Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties) => Task.CompletedTask;
    }

    // Resolves the subject and the dynamic-provider options cache the way a real host wires them:
    // AddIdentityServer registers IdentityProviderOptionsMonitorCache as a singleton and, via
    // AddOidcDynamicProvider, registers IOptionsMonitorCache<OpenIdConnectOptions> as a
    // SpaceAwareDynamicOptionsMonitorCache<OpenIdConnectOptions> wired to the real
    // ISpaceContextAccessor. The test's own HttpContextAccessor instance is registered last so it
    // wins resolution and the test can swap its HttpContext between simulated requests.
    private static (
        IdentityProviderOptionsMonitorCache Subject,
        IOptionsMonitorCache<OpenIdConnectOptions> OptionsCache,
        ISpaceContextAccessor Accessor) CreateSubject(HttpContextAccessor httpContextAccessor) =>
        CreateSubject(httpContextAccessor, null, null);

    private static (
        IdentityProviderOptionsMonitorCache Subject,
        IOptionsMonitorCache<OpenIdConnectOptions> OptionsCache,
        ISpaceContextAccessor Accessor) CreateSubject(
            HttpContextAccessor httpContextAccessor,
            TimeProvider? timeProvider,
            TimeSpan? cacheIdleTimeout)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        if (timeProvider != null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddIdentityServer(options =>
            {
                if (cacheIdleTimeout.HasValue)
                {
                    options.DynamicProviders.CacheIdleTimeout = cacheIdleTimeout.Value;
                }
            })
            .AddSpaces(_ => { });
        services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);

        var provider = services.BuildServiceProvider();

        var subject = provider.GetRequiredService<IdentityProviderOptionsMonitorCache>();
        var optionsCache = provider.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();

        return (subject, optionsCache, accessor);
    }

    private static OidcProvider CreateIdentityProvider(string scheme, string authority) => new()
    {
        Scheme = scheme,
        Authority = authority,
        ClientId = "client",
        ClientSecret = "secret",
        ResponseType = "code",
        Scope = "openid profile"
    };

    [Fact]
    public void EnsureCacheUpdated_for_changed_provider_in_space_a_evicts_only_space_as_options_never_space_bs()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);

        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();

        using (accessor.SetSpace(spaceA))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });
        }

        using (accessor.SetSpace(spaceB))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://b1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://b1" });
        }

        using (accessor.SetSpace(spaceA))
        {
            var updated = subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a2"));

            updated.ShouldBeTrue();
            optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeTrue();
        }

        using (accessor.SetSpace(spaceB))
        {
            optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeFalse();
        }
    }

    [Fact]
    public void EnsureCacheUpdated_within_space_unchanged_provider_does_not_evict()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);

        using var scope = accessor.SetSpace(SpaceId.New());

        subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
        optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });

        var updated = subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1"));

        updated.ShouldBeFalse();
        optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeFalse();
    }

    [Fact]
    public void EnsureCacheUpdated_within_space_changed_provider_evicts()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);

        using var scope = accessor.SetSpace(SpaceId.New());

        subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
        optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });

        var updated = subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a2"));

        updated.ShouldBeTrue();
        optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeTrue();
    }

    [Fact]
    public void Remove_scheme_in_space_a_removes_space_as_tracker_and_options_leaves_space_b_and_unqualified_intact()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);

        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();

        using (accessor.SetSpace(spaceA))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });
        }

        using (accessor.SetSpace(spaceB))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://b1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://b1" });
        }

        subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://none1")).ShouldBeFalse();
        optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://none1" });

        // Remove in space A only.
        using (accessor.SetSpace(spaceA))
        {
            subject.Remove("my-oidc");

            // Space A's options were evicted.
            optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeTrue();
            // Space A's tracker entry was cleared: a changed provider is now a fresh observation
            // (returns false, "added"), not an update (which would return true).
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a2")).ShouldBeFalse();
        }

        // Space B's options and tracker entry are untouched.
        using (accessor.SetSpace(spaceB))
        {
            optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeFalse();
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://b2")).ShouldBeTrue();
        }

        // The unqualified (no-space) options and tracker entry are untouched.
        optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeFalse();
        subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://none2")).ShouldBeTrue();
    }
    [Fact]
    public void Remove_many_distinct_nonexistent_scheme_names_does_not_add_tracker_or_options_entries()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);

        using var scope = accessor.SetSpace(SpaceId.New());

        var names = Enumerable.Range(0, 50).Select(i => $"missing-{i}").ToArray();

        foreach (var name in names)
        {
            subject.Remove(name);
        }

        foreach (var name in names)
        {
            optionsCache.TryAdd(name, new OpenIdConnectOptions()).ShouldBeTrue();
        }

        subject.EnsureCacheUpdated(CreateIdentityProvider("missing-0", "https://first")).ShouldBeFalse();
    }

    [Fact]
    public void Remove_with_name_equal_to_static_scheme_in_configured_space_does_not_evict_static_options()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);

        using var scope = accessor.SetSpace(SpaceId.New());

        optionsCache.TryAdd("static-scheme", new OpenIdConnectOptions()).ShouldBeTrue();

        subject.Remove("static-scheme");

        optionsCache.TryAdd("static-scheme", new OpenIdConnectOptions()).ShouldBeFalse();
    }

    [Fact]
    public void Remove_with_name_equal_to_static_scheme_without_configured_space_does_not_evict_static_options()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var (subject, optionsCache, _) = CreateSubject(httpContextAccessor);

        optionsCache.TryAdd("static-scheme", new OpenIdConnectOptions()).ShouldBeTrue();

        subject.Remove("static-scheme");

        optionsCache.TryAdd("static-scheme", new OpenIdConnectOptions()).ShouldBeFalse();
    }

    [Fact]
    public void TryRemoveDynamic_foo_in_space_a_removes_only_configured_dynamic_entry_leaves_static_composed_name_entry_intact()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var spaceAId = SpaceId.New();
        var composedName = $"{spaceAId.Value}_foo";
        MarkDynamic(schemeCache, "foo");

        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);
        using var scope = accessor.SetSpace(spaceAId);

        // Static entry literally named "{spaceAId}_foo" lands in the unqualified store.
        optionsCache.TryAdd(composedName, new OpenIdConnectOptions()).ShouldBeTrue();

        subject.EnsureCacheUpdated(CreateIdentityProvider("foo", "https://foo1")).ShouldBeFalse();
        optionsCache.GetOrAdd("foo", () => new OpenIdConnectOptions { Authority = "https://foo1" });

        subject.Remove("foo");

        optionsCache.TryAdd("foo", new OpenIdConnectOptions()).ShouldBeTrue();
        optionsCache.TryAdd(composedName, new OpenIdConnectOptions()).ShouldBeFalse();
    }

    [Fact]
    public void TryRemove_static_composed_name_removes_only_static_entry_leaves_configured_dynamic_foo_entry_intact()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var spaceAId = SpaceId.New();
        var composedName = $"{spaceAId.Value}_foo";
        MarkDynamic(schemeCache, "foo");

        var (_, optionsCache, accessor) = CreateSubject(httpContextAccessor);
        using var scope = accessor.SetSpace(spaceAId);

        optionsCache.TryAdd(composedName, new OpenIdConnectOptions()).ShouldBeTrue();
        optionsCache.GetOrAdd("foo", () => new OpenIdConnectOptions { Authority = "https://foo1" });

        // An ordinary static TryRemove for a scheme literally named the composed key.
        optionsCache.TryRemove(composedName).ShouldBeTrue();

        optionsCache.TryAdd(composedName, new OpenIdConnectOptions()).ShouldBeTrue();
        var stillCached = optionsCache.GetOrAdd("foo", () => new OpenIdConnectOptions { Authority = "https://should-not-be-used" });
        stillCached.Authority.ShouldBe("https://foo1");
    }

    [Fact]
    public void EnsureCacheUpdated_tracker_partition_collision_between_qualified_scheme_and_unqualified_composed_name_is_isolated()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        var spaceAId = SpaceId.New();
        var composedName = $"{spaceAId.Value}_foo";
        MarkDynamic(schemeCache, "foo");

        var (subject, _, accessor) = CreateSubject(httpContextAccessor);

        // Tracker partition key is a (SpaceId?, string) tuple, so string-collision cannot occur.
        using (accessor.SetSpace(spaceAId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("foo", "https://foo1")).ShouldBeFalse();
        }

        subject.EnsureCacheUpdated(CreateIdentityProvider(composedName, "https://composed1")).ShouldBeFalse();

        using (accessor.SetSpace(spaceAId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("foo", "https://foo2")).ShouldBeTrue();
        }

        subject.EnsureCacheUpdated(CreateIdentityProvider(composedName, "https://composed1")).ShouldBeFalse();
    }

    [Fact]
    public void idle_tracker_and_options_entries_are_swept_after_idle_timeout_elapses()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor, time, idleTimeout);

        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });
        }

        time.Advance(idleTimeout + idleTimeout);

        // Trigger a sweep via another space's call.
        using (accessor.SetSpace(SpaceId.New()))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://trigger")).ShouldBeFalse();
        }

        using (accessor.SetSpace(spaceId))
        {
            // Tracker entry was swept: a changed provider is treated as a first observation
            // (returns false), not an update.
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a2")).ShouldBeFalse();

            // Options entry was swept too.
            optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeTrue();
        }
    }

    [Fact]
    public void tracker_entry_touched_by_get_latest_provider_within_idle_timeout_survives_a_sweep()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var (subject, _, accessor) = CreateSubject(httpContextAccessor, time, idleTimeout);
        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
        }

        for (var i = 0; i < 6; i++)
        {
            time.Advance(TimeSpan.FromMinutes(4));
            using (accessor.SetSpace(spaceId))
            {
                subject.GetLatestProvider("my-oidc").ShouldNotBeNull();
            }
        }

        using (accessor.SetSpace(spaceId))
        {
            // Still the original entry: an unchanged provider does not evict options, which it
            // only would if the tracker had been swept and treated this as a first observation.
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            subject.GetLatestProvider("my-oidc").ShouldNotBeNull();
        }
    }

    [Fact]
    public void tracker_entry_touched_between_sweeps_idle_check_and_eviction_cas_survives_the_sweep()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor, time, idleTimeout);
        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });
        }

        time.Advance(idleTimeout + idleTimeout);

        // Forces the interleaving the fix closes: sweep decided the entry idle but hasn't CAS'd
        // it to evicted yet; a lookup touches it in that window, which must make the CAS fail.
        subject.OnEntryDecidedIdle = entry => entry.Touch(time);

        using (accessor.SetSpace(spaceId))
        {
            // Still treated as an unchanged provider (not a first observation), proving the
            // tracker entry survived the race.
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
        }
    }

    [Fact]
    public void ensure_cache_updated_after_idle_sweep_treats_unchanged_provider_as_first_observation()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor, time, idleTimeout);
        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });
        }

        time.Advance(idleTimeout + idleTimeout);

        using (accessor.SetSpace(SpaceId.New()))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://trigger")).ShouldBeFalse();
        }

        using (accessor.SetSpace(spaceId))
        {
            // Even for the *same* provider the entry was tracking, the swept-away entry must be
            // treated as "not seen": first-observation semantics (returns false, options are
            // freshly evictable).
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeTrue();
        }
    }

    [Fact]
    public void get_latest_provider_after_idle_sweep_returns_null()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var time = new FakeTimeProvider();
        var idleTimeout = TimeSpan.FromMinutes(10);
        var (subject, _, accessor) = CreateSubject(httpContextAccessor, time, idleTimeout);
        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
        }

        time.Advance(idleTimeout + idleTimeout);

        using (accessor.SetSpace(SpaceId.New()))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://trigger")).ShouldBeFalse();
        }

        using (accessor.SetSpace(spaceId))
        {
            subject.GetLatestProvider("my-oidc").ShouldBeNull();
        }
    }

    [Fact]
    public void infinite_cache_idle_timeout_disables_tracker_expiry()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var time = new FakeTimeProvider();
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor, time, Timeout.InfiniteTimeSpan);
        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });
        }

        time.Advance(TimeSpan.FromDays(365));

        using (accessor.SetSpace(SpaceId.New()))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://trigger")).ShouldBeFalse();
        }

        using (accessor.SetSpace(spaceId))
        {
            // Still treated as an update (not a first observation), so the tracker entry survived.
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a2")).ShouldBeTrue();
        }
    }

    [Fact]
    public void ensure_cache_updated_for_an_already_evicted_entry_treats_unchanged_provider_as_first_observation_and_keeps_a_live_entry()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var (subject, optionsCache, accessor) = CreateSubject(httpContextAccessor);
        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.GetOrAdd("my-oidc", () => new OpenIdConnectOptions { Authority = "https://a1" });
        }

        // Mark the tracker entry evicted in place, without going through the sweep (which would
        // also remove it from the dictionary), so EnsureCacheUpdated observes a dictionary entry
        // whose Touch fails, independent of any sweep timing.
        var entry = subject.GetEntryForTest(spaceId, "my-oidc");
        entry.ShouldNotBeNull();
        entry.TryMarkEvicted(entry.LastUsedTicksRaw).ShouldBeTrue();

        using (accessor.SetSpace(spaceId))
        {
            // Unchanged provider, but the entry was evicted: must be treated as a first
            // observation (returns false) and must evict the options entry too.
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
            optionsCache.TryAdd("my-oidc", new OpenIdConnectOptions()).ShouldBeTrue();

            // A fresh, live entry must have been installed in place of the evicted one.
            subject.GetEntryForTest(spaceId, "my-oidc").ShouldNotBeNull();
        }
    }

    [Fact]
    public void get_latest_provider_for_an_already_evicted_entry_returns_null()
    {
        var (httpContextAccessor, schemeCache) = BuildHttpContext();
        MarkDynamic(schemeCache, "my-oidc");
        var (subject, _, accessor) = CreateSubject(httpContextAccessor);
        var spaceId = SpaceId.New();

        using (accessor.SetSpace(spaceId))
        {
            subject.EnsureCacheUpdated(CreateIdentityProvider("my-oidc", "https://a1")).ShouldBeFalse();
        }

        var entry = subject.GetEntryForTest(spaceId, "my-oidc");
        entry.ShouldNotBeNull();
        entry.TryMarkEvicted(entry.LastUsedTicksRaw).ShouldBeTrue();

        using (accessor.SetSpace(spaceId))
        {
            subject.GetLatestProvider("my-oidc").ShouldBeNull();
        }
    }
}

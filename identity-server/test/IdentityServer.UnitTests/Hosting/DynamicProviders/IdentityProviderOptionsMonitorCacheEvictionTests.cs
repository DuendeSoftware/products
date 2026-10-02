// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

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
        ISpaceContextAccessor Accessor) CreateSubject(HttpContextAccessor httpContextAccessor)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddIdentityServer();
        services.AddSpaces();
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
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Hosting.DynamicProviders.Store;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Hosting.DynamicProviders;

public class IdentityProviderOptionsMonitorCacheTests
{
    [Fact]
    public void ensure_cache_updated_should_remove_options_for_first_observation()
    {
        var (subject, optionsCache) = CreateSubject();
        var identityProvider = CreateIdentityProvider();

        optionsCache.TryAdd(identityProvider.Scheme, new OpenIdConnectOptions());

        var updated = subject.EnsureCacheUpdated(identityProvider);

        updated.ShouldBeFalse();
        optionsCache.TryRemove(identityProvider.Scheme).ShouldBeFalse();
    }

    [Fact]
    public void ensure_cache_updated_should_not_remove_options_for_unchanged_provider()
    {
        var (subject, optionsCache) = CreateSubject();
        var identityProvider = CreateIdentityProvider();

        subject.EnsureCacheUpdated(identityProvider);
        optionsCache.TryAdd(identityProvider.Scheme, new OpenIdConnectOptions());

        var updated = subject.EnsureCacheUpdated(CreateIdentityProvider());

        updated.ShouldBeFalse();
        optionsCache.TryRemove(identityProvider.Scheme).ShouldBeTrue();
    }

    [Fact]
    public void ensure_cache_updated_should_remove_options_for_updated_provider()
    {
        var (subject, optionsCache) = CreateSubject();
        var identityProvider = CreateIdentityProvider();

        subject.EnsureCacheUpdated(identityProvider);
        optionsCache.TryAdd(identityProvider.Scheme, new OpenIdConnectOptions());

        var updatedIdentityProvider = CreateIdentityProvider();
        updatedIdentityProvider.Authority = "https://idp2";

        var updated = subject.EnsureCacheUpdated(updatedIdentityProvider);

        updated.ShouldBeTrue();
        optionsCache.TryRemove(identityProvider.Scheme).ShouldBeFalse();
    }

    [Fact]
    public void ensure_cache_updated_should_treat_property_order_as_equal()
    {
        var (subject, optionsCache) = CreateSubject();
        var identityProvider = CreateIdentityProvider();
        identityProvider.Properties["z"] = "last";
        identityProvider.Properties["a"] = "first";

        var reorderedIdentityProvider = CreateIdentityProvider();
        reorderedIdentityProvider.Properties["a"] = "first";
        reorderedIdentityProvider.Properties["z"] = "last";

        subject.EnsureCacheUpdated(identityProvider);
        optionsCache.TryAdd(identityProvider.Scheme, new OpenIdConnectOptions());

        var updated = subject.EnsureCacheUpdated(reorderedIdentityProvider);

        updated.ShouldBeFalse();
        optionsCache.TryRemove(identityProvider.Scheme).ShouldBeTrue();
    }

    private static (IdentityProviderOptionsMonitorCache Subject, IOptionsMonitorCache<OpenIdConnectOptions> OptionsCache) CreateSubject()
    {
        var options = TestIdentityServerOptions.Create();
        options.DynamicProviders.AddProviderType<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider>("oidc");

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<IOptionsMonitorCache<OpenIdConnectOptions>, OptionsCache<OpenIdConnectOptions>>();

        var serviceProvider = services.BuildServiceProvider();
        var subject = new IdentityProviderOptionsMonitorCache(serviceProvider, options);
        var optionsCache = serviceProvider.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();

        return (subject, optionsCache);
    }

    private static OidcProvider CreateIdentityProvider() => CreateIdentityProvider("scheme");

    private static OidcProvider CreateIdentityProvider(string scheme) => new()
    {
        Scheme = scheme,
        Authority = "https://idp1",
        ClientId = "client",
        ClientSecret = "secret",
        ResponseType = "code",
        Scope = "openid profile"
    };

    private static (IdentityProviderOptionsMonitorCache Subject, FakeTimeProvider TimeProvider) CreateSubjectWithIdleTimeout(TimeSpan idleTimeout)
    {
        var options = TestIdentityServerOptions.Create();
        options.DynamicProviders.AddProviderType<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider>("oidc");
        options.DynamicProviders.CacheIdleTimeout = idleTimeout;

        var time = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<TimeProvider>(time);
        services.AddSingleton<IOptionsMonitorCache<OpenIdConnectOptions>, OptionsCache<OpenIdConnectOptions>>();

        var serviceProvider = services.BuildServiceProvider();
        var subject = new IdentityProviderOptionsMonitorCache(serviceProvider, options);

        return (subject, time);
    }

    [Fact]
    public void sweep_does_not_run_again_within_a_quarter_of_the_idle_timeout_after_the_previous_sweep()
    {
        var idleTimeout = TimeSpan.FromMinutes(10);
        var (subject, time) = CreateSubjectWithIdleTimeout(idleTimeout);

        subject.EnsureCacheUpdated(CreateIdentityProvider("e-scheme")); // t = 0, first observation

        // t = 9.9min: triggers a sweep (sweep interval, timeout/4 = 2.5min, has elapsed). The
        // e-scheme entry is not yet idle (9.9min < 10min), so it survives, and this call becomes
        // the "last sweep" timestamp. A distinct scheme's first observation is used as the
        // trigger so it does not touch e-scheme's entry.
        time.Advance(TimeSpan.FromMinutes(9.9));
        subject.EnsureCacheUpdated(CreateIdentityProvider("trigger-1"));
        subject.IsTracked(SpaceId.Default, "e-scheme").ShouldBeTrue();

        // t = 10.1min: e-scheme is now idle (10.1min >= 10min), but only 0.2min have passed
        // since the last sweep (< 2.5min sweep interval). The throttle must suppress this sweep
        // entirely, so e-scheme must survive even though it would be evicted if a sweep ran.
        time.Advance(TimeSpan.FromMinutes(0.2));
        subject.EnsureCacheUpdated(CreateIdentityProvider("trigger-2"));
        subject.IsTracked(SpaceId.Default, "e-scheme").ShouldBeTrue(
            "the throttle must suppress a sweep within timeout/4 of the previous sweep");

        // t = 12.5min: 2.6min have now passed since the last sweep (>= 2.5min), so a sweep runs
        // again, and e-scheme (now 12.5min idle) must finally be removed.
        time.Advance(TimeSpan.FromMinutes(2.4));
        subject.EnsureCacheUpdated(CreateIdentityProvider("trigger-3"));
        subject.IsTracked(SpaceId.Default, "e-scheme").ShouldBeFalse();
    }

    [Fact]
    public void sweep_idle_entries_if_due_removes_idle_entries_from_every_registered_dynamic_options_cache()
    {
        var idleTimeout = TimeSpan.FromMinutes(10);
        var time = new FakeTimeProvider();

        var options = TestIdentityServerOptions.Create();
        options.DynamicProviders.AddProviderType<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider>("oidc");
        options.DynamicProviders.AddProviderType<Saml2Handler, Saml2Options, SamlProvider>("saml");
        options.DynamicProviders.CacheIdleTimeout = idleTimeout;

        var schemeCache = new DynamicAuthenticationSchemeCache();
        schemeCache.Add("oidc-scheme", new DynamicAuthenticationScheme(new IdentityProvider("oidc") { Scheme = "oidc-scheme" }, typeof(OpenIdConnectHandler)));
        schemeCache.Add("saml-scheme", new DynamicAuthenticationScheme(new IdentityProvider("saml") { Scheme = "saml-scheme" }, typeof(Saml2Handler)));

        var httpContextServices = new ServiceCollection();
        httpContextServices.AddSingleton(schemeCache);
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { RequestServices = httpContextServices.BuildServiceProvider() }
        };

        var oidcOptionsCache = new SpaceAwareDynamicOptionsMonitorCache<OpenIdConnectOptions>(null, httpContextAccessor, time, idleTimeout);
        var samlOptionsCache = new SpaceAwareDynamicOptionsMonitorCache<Saml2Options>(null, httpContextAccessor, time, idleTimeout);

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<TimeProvider>(time);
        services.AddSingleton<IOptionsMonitorCache<OpenIdConnectOptions>>(oidcOptionsCache);
        services.AddSingleton<IOptionsMonitorCache<Saml2Options>>(samlOptionsCache);

        var serviceProvider = services.BuildServiceProvider();
        var subject = new IdentityProviderOptionsMonitorCache(serviceProvider, options);

        oidcOptionsCache.GetOrAdd("oidc-scheme", () => new OpenIdConnectOptions()); // t = 0
        samlOptionsCache.GetOrAdd("saml-scheme", () => new Saml2Options()); // t = 0

        time.Advance(idleTimeout + idleTimeout);

        // Any request through the middleware calls this, regardless of which (if any) dynamic
        // scheme it resolves; it must reach both registered options caches.
        subject.SweepIdleEntriesIfDue();

        oidcOptionsCache.ContainsDynamic(SpaceId.Default, "oidc-scheme").ShouldBeFalse(
            "the OIDC options cache's idle entry must have been swept by the tracker's coordinated sweep");
        samlOptionsCache.ContainsDynamic(SpaceId.Default, "saml-scheme").ShouldBeFalse(
            "the SAML options cache's idle entry must have been swept by the tracker's coordinated sweep");
    }
}

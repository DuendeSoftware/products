// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Hosting.DynamicProviders;

public class IdentityProviderOptionsMonitorCacheTests
{
    [Fact]
    public void ensure_cache_updated_should_not_remove_options_for_first_observation()
    {
        var (subject, optionsCache) = CreateSubject();
        var identityProvider = CreateIdentityProvider();

        optionsCache.TryAdd(identityProvider.Scheme, new OpenIdConnectOptions());

        var updated = subject.EnsureCacheUpdated(identityProvider);

        updated.ShouldBeFalse();
        optionsCache.TryRemove(identityProvider.Scheme).ShouldBeTrue();
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

    private static OidcProvider CreateIdentityProvider() => new()
    {
        Scheme = "scheme",
        Authority = "https://idp1",
        ClientId = "client",
        ClientSecret = "secret",
        ResponseType = "code",
        Scope = "openid profile"
    };
}

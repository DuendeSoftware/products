// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders.Store;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UnitTests.Hosting.DynamicProviders;

public class DynamicProviderOptionsMonitorCacheRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();

        var builder = services.AddIdentityServer();
        builder.AddSamlDynamicProvider();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void oidc_options_monitor_cache_resolves_to_space_aware_decorator()
    {
        using var provider = BuildProvider();

        var cache = provider.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();

        cache.ShouldBeOfType<SpaceAwareDynamicOptionsMonitorCache<OpenIdConnectOptions>>();
    }

    [Fact]
    public void saml_options_monitor_cache_resolves_to_space_aware_decorator()
    {
        using var provider = BuildProvider();

        var cache = provider.GetRequiredService<IOptionsMonitorCache<Saml2Options>>();

        cache.ShouldBeOfType<SpaceAwareDynamicOptionsMonitorCache<Saml2Options>>();
    }
}

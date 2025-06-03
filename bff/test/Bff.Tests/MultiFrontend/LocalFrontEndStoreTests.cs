// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Duende.Bff.Tests.MultiFrontend;

public class LocalFrontEndStoreTests
{
    private readonly BffOptions _bffOptions = new();
    private readonly TestOptionsMonitor<BffConfiguration> _bffConfigurationOptionsMonitor = new(new BffConfiguration());
    private BffFrontend[]? _frontendsConfiguredDuringStartup;

    protected TestData The = new();
    protected TestDataBuilder Some => new(The);

    [Fact]
    public void Can_load_frontends_from_constructor()
    {
        _frontendsConfiguredDuringStartup = [
            Some.BffFrontendWithSelectionCriteria(),
            Some.BffFrontendWithSelectionCriteria() with { Name = BffFrontendName.Parse("different") }
        ];

        var cache = BuildCache();

        var result = cache.GetAll();
        result.Count.ShouldBe(2);
        result.ShouldBeEquivalentTo(_frontendsConfiguredDuringStartup.AsReadOnly());
    }

    private LocalFrontendStore BuildCache()
    {
        // No longer inject OptionsCache
        var cache = new LocalFrontendStore(_bffConfigurationOptionsMonitor, _frontendsConfiguredDuringStartup);
        return cache;
    }

    [Fact]
    public void Can_load_frontends_from_config()
    {
        _bffConfigurationOptionsMonitor.CurrentValue = new BffConfiguration()
        {
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                [The.FrontendName] = Some.BffFrontendConfiguration(),
                ["different"] = Some.BffFrontendConfiguration()
            }
        };

        var cache = BuildCache();
        var result = cache.GetAll();
        result.Count.ShouldBe(2);

        result.ShouldBe(new[]
        {
            Some.BffFrontendWithSelectionCriteria(),
            Some.BffFrontendWithSelectionCriteria() with { Name = BffFrontendName.Parse("different") }
        }.AsReadOnly());

        result.First().Proxy.RemoteApis.Length.ShouldBe(1);
        result.First().Proxy.RemoteApis[0].AccessTokenRetrieverType.ShouldBe(The.TokenRetrieverType);
        result.First().Proxy.RemoteApis[0].Parameters!.ToUserAccessTokenRequestParameters()
            .ShouldBeEquivalentTo(Some.BffUserAccessTokenParameters().ToUserAccessTokenRequestParameters());
    }

    [Fact(Skip = "find other way of testing this")]
    public void ODIC_Config_precedence_is_programmatic_defaults_then_configured_defaults_then_frontend_specific()
    {
        _bffOptions.ConfigureOpenIdConnectDefaults = opt =>
        {
            opt.ClientId = "clientid from programmatic defaults";
            opt.ClientSecret = "clientsecret from programmatic defaults";
            opt.ResponseMode = "responsemode from programmatic defaults";
        };

        _bffConfigurationOptionsMonitor.CurrentValue = new BffConfiguration()
        {
            DefaultOidcSettings = new OidcConfiguration()
            {
                ClientSecret = "clientsecret from configured defaults",
                ResponseMode = "responsemode from configured defaults",
            },
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                [The.FrontendName] = new BffFrontendConfiguration()
                {
                    Oidc = new OidcConfiguration()
                    {
                        ResponseMode = "responsemode from frontend",
                    }
                }
            }
        };

        var cache = BuildCache();
        var result = cache.GetAll();
        var openIdConnectOptions = new OpenIdConnectOptions();
        result.First().ConfigureOpenIdConnectOptions!.Invoke(openIdConnectOptions);
        openIdConnectOptions.ClientId.ShouldBe("clientid from programmatic defaults");
        openIdConnectOptions.ClientSecret.ShouldBe("clientsecret from configured defaults");
        openIdConnectOptions.ResponseMode.ShouldBe("responsemode from frontend");
    }
    [Fact(Skip = "find other way of testing this")]
    public void Cookie_Config_precedence_is_programmatic_defaults_then_configured_defaults_then_frontend_specific()
    {
        _bffOptions.ConfigureCookieDefaults = opt =>
        {
            opt.Cookie.Name = "Name from programmatic defaults";
            opt.Cookie.Path = "Path from programmatic defaults";
            opt.Cookie.Domain = "Domain from programmatic defaults";
        };

        _bffConfigurationOptionsMonitor.CurrentValue = new BffConfiguration()
        {
            DefaultCookieSettings = new CookieConfiguration()
            {
                Path = "Path from configured defaults",
                Domain = "Domain from configured defaults",
            },
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                [The.FrontendName] = new BffFrontendConfiguration()
                {
                    Cookies = new CookieConfiguration()
                    {
                        Domain = "Domain from frontend",
                    }
                }
            }
        };

        var cache = BuildCache();
        var result = cache.GetAll();
        var cookieOptions = new CookieAuthenticationOptions();
        result.First().ConfigureCookieOptions!.Invoke(cookieOptions);
        cookieOptions.Cookie.Name.ShouldBe("Name from programmatic defaults");
        cookieOptions.Cookie.Path.ShouldBe("Path from configured defaults");
        cookieOptions.Cookie.Domain.ShouldBe("Domain from frontend");
    }


    [Fact]
    public void When_frontend_is_updated_then_event_is_raised()
    {
        var cache = BuildCache();
        var bffFrontend = Some.BffFrontendWithSelectionCriteria();

        // Track event invocations
        BffFrontend? eventArg = null;
        var eventCount = 0;
        cache.OnFrontendChanged += f => { eventArg = f; eventCount++; };

        // Add a new frontend (should not raise event, as it's an add)
        cache.AddOrUpdate(bffFrontend);
        eventCount.ShouldBe(0);

        // Update existing frontend (should raise event)
        var updatedFrontend = bffFrontend with { IndexHtmlUrl = new Uri("https://different") };
        cache.AddOrUpdate(updatedFrontend);

        eventCount.ShouldBe(1);
        eventArg.ShouldNotBeNull();
        eventArg.ShouldBe(bffFrontend);
    }

    [Fact]
    public void When_frontend_is_removed_then_event_is_raised()
    {
        var cache = BuildCache();
        var bffFrontend = Some.BffFrontendWithSelectionCriteria();

        // Track event invocations
        BffFrontend? eventArg = null;
        var eventCount = 0;
        cache.OnFrontendChanged += f => { eventArg = f; eventCount++; };

        // Add a new frontend (should not raise event)
        cache.AddOrUpdate(bffFrontend);
        eventCount.ShouldBe(0);

        // Remove frontend (should raise event)
        cache.Remove(bffFrontend.Name);

        eventCount.ShouldBe(1);
        eventArg.ShouldNotBeNull();
        eventArg.ShouldBe(bffFrontend);
    }
}

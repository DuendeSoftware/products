// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Duende.Bff.Tests.MultiFrontend;

public class FrontendCollectionTests
{
    private readonly BffOptions _bffOptions = new();
    private readonly TestOptionsMonitor<BffConfiguration> _bffConfigurationOptionsMonitor = new(new BffConfiguration());
    private BffFrontend[]? _frontendsConfiguredDuringStartup;

    internal TestData The = new();
    internal TestDataBuilder Some => new(The);

    [Fact]
    public void Can_load_frontends_from_constructor()
    {
        _frontendsConfiguredDuringStartup =
        [
            Some.BffFrontendWithMatchingCriteria(),
            Some.BffFrontendWithMatchingCriteria() with { Name = BffFrontendName.Parse("different") }
        ];

        var cache = BuildFrontendCollection();

        var result = cache;
        result.Count.ShouldBe(2);
        result.ToArray().ShouldBeEquivalentTo(_frontendsConfiguredDuringStartup);
    }

    private FrontendCollection BuildFrontendCollection()
    {
        // No longer inject OptionsCache
        var cache = new FrontendCollection(Some.LicenseValidator,
            _bffConfigurationOptionsMonitor, [], _frontendsConfiguredDuringStartup);
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

        var cache = BuildFrontendCollection();
        var result = cache;
        result.Count.ShouldBe(2);

        result.ShouldBe(new[]
        {
            Some.BffFrontendWithMatchingCriteria(),
            Some.BffFrontendWithMatchingCriteria() with { Name = BffFrontendName.Parse("different") }
        }.AsReadOnly());
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

        var cache = BuildFrontendCollection();
        var openIdConnectOptions = new OpenIdConnectOptions();
        cache.First().ConfigureOpenIdConnectOptions!.Invoke(openIdConnectOptions);
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

        var cache = BuildFrontendCollection();
        var cookieOptions = new CookieAuthenticationOptions();
        cache.First().ConfigureCookieOptions!.Invoke(cookieOptions);
        cookieOptions.Cookie.Name.ShouldBe("Name from programmatic defaults");
        cookieOptions.Cookie.Path.ShouldBe("Path from configured defaults");
        cookieOptions.Cookie.Domain.ShouldBe("Domain from frontend");
    }


    [Fact]
    public void When_frontend_is_updated_then_event_is_raised()
    {
        var cache = BuildFrontendCollection();
        var bffFrontend = Some.BffFrontendWithMatchingCriteria();

        // Track event invocations
        BffFrontend? eventArg = null;
        var addedCount = 0;
        var updateCount = 0;
        cache.OnFrontendChanged += f =>
        {
            eventArg = f;
            updateCount++;
        };
        cache.OnFrontendAdded += f =>
        {
            eventArg = f;
            addedCount++;
        };

        cache.AddOrUpdate(bffFrontend);
        addedCount.ShouldBe(1);
        updateCount.ShouldBe(0);

        var updatedFrontend = bffFrontend with { CdnIndexHtmlUrl = new Uri("https://different") };
        cache.AddOrUpdate(updatedFrontend);

        updateCount.ShouldBe(1);
        addedCount.ShouldBe(1);
        eventArg.ShouldNotBeNull();
        eventArg.ShouldBe(updatedFrontend);
    }

    [Fact]
    public void When_identical_frontend_is_updated_then_no_event_is_raised()
    {
        var cache = BuildFrontendCollection();
        var bffFrontend = Some.BffFrontendWithMatchingCriteria() with
        {
            ConfigureOpenIdConnectOptions = null,
            ConfigureCookieOptions = null
        };

        // Track event invocations
        BffFrontend? eventArg = null;
        var addedCount = 0;
        var updateCount = 0;
        cache.OnFrontendChanged += f =>
        {
            eventArg = f;
            updateCount++;
        };
        cache.OnFrontendAdded += f =>
        {
            eventArg = f;
            addedCount++;
        };
        cache.AddOrUpdate(bffFrontend);
        addedCount.ShouldBe(1);
        updateCount.ShouldBe(0);


        var updatedFrontend = bffFrontend with { };
        cache.AddOrUpdate(updatedFrontend);

        updateCount.ShouldBe(0);
        addedCount.ShouldBe(1);
        eventArg.ShouldNotBeNull();
        eventArg.ShouldBe(updatedFrontend);
    }

    [Fact]
    public void When_frontend_is_removed_then_event_is_raised()
    {
        var cache = BuildFrontendCollection();
        var bffFrontend = Some.BffFrontendWithMatchingCriteria();

        // Track event invocations
        BffFrontend? eventArg = null;
        var eventCount = 0;
        cache.OnFrontendChanged += f =>
        {
            eventArg = f;
            eventCount++;
        };

        //// Add a new frontend
        cache.AddOrUpdate(bffFrontend);

        // Remove frontend (should raise event)
        cache.Remove(bffFrontend.Name);

        eventCount.ShouldBe(1);
        eventArg.ShouldNotBeNull();
        eventArg.ShouldBe(bffFrontend);
    }
}

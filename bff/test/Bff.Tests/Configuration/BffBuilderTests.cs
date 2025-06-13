// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Duende.Bff.Yarp.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Tests.Configuration;

public class BffBuilderTests
{
    public TestData The = new TestData();
    internal TestDataBuilder Some => new TestDataBuilder(The);

    [Fact]
    public void Can_add_multiple_frontends()
    {
        var services = new ServiceCollection();
        var frontend1 = Some.BffFrontend();
        var frontend2 = Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("different")
        };
        services.AddBff()
            .AddFrontends(frontend1, frontend2);

        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(2);
        frontends.ShouldContain(frontend1);
        frontends.ShouldContain(frontend2);
    }

    [Fact]
    public void Can_add_frontends_in_multiple_calls()
    {
        var services = new ServiceCollection();
        var frontend1 = Some.BffFrontend();
        var frontend2 = Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("different")
        };
        services.AddBff()
            .AddFrontends(frontend1)
            .AddFrontends(frontend2);

        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(2);
        frontends.ShouldContain(frontend1);
        frontends.ShouldContain(frontend2);
    }

    [Fact]
    public void Can_load_frontend_with_all_possible_values_from_configuration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                Frontends = new Dictionary<string, BffFrontendConfiguration>()
                {
                    [The.FrontendName] = Some.BffFrontendConfiguration(),
                }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBff()
            .LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(1);

        var found = frontends.First(x => x.Name == The.FrontendName);
        var expected = Some.BffFrontend() with
        {
            Name = The.FrontendName,
            IndexHtmlUrl = The.Url,
            SelectionCriteria = new FrontendSelectionCriteria()
            {
                MatchingOrigin = The.Origin,
                MatchingPath = The.Path,
            },
        };
        found.ShouldBe(expected);

        // Verify that the oidc options built by the frontend are equivalent to the expected one
        var builtOptions = new OpenIdConnectOptions();
        found.ConfigureOpenIdConnectOptions!(builtOptions);
        ValidateOpenIdConnectOptions(builtOptions, The.CallbackPath.ToString());

        // verify that the cookie options built by the frontend are equivalent to the expected one
        var cookieOptions = new CookieAuthenticationOptions();
        if (found.ConfigureCookieOptions != null)
        {
            found.ConfigureCookieOptions(cookieOptions);
        }

        ValidateCookieOptions(cookieOptions);
    }


    [Fact]
    public void Can_load_frontend_with_proxy_config()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new ProxyConfiguration()
            {
                Frontends = new()
                {
                    [The.FrontendName] = Some.FrontendProxyConfiguration()
                }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBff()
            .AddRemoteApis()
            .LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(1);

        var found = frontends.First(x => x.Name == The.FrontendName);

        found.DataExtensions.OfType<ProxyBffPlugin>().Count().ShouldBe(1);
        var proxyBffDataExtensions = found.DataExtensions.OfType<ProxyBffPlugin>().First();

        proxyBffDataExtensions.RemoteApis[0].ShouldBe(Some.RemoteApi());
    }


    [Fact]
    public void Can_load_frontend_with_minimal_values_from_configuration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                Frontends = new Dictionary<string, BffFrontendConfiguration>()
                {
                    [The.FrontendName] = new(),

                }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBff().LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(1);

        var found = frontends.First(x => x.Name == The.FrontendName);
        found.ShouldBe(new BffFrontend
        {
            Name = The.FrontendName,
        });
    }

    [Fact]
    public void Can_load_frontend_with_all_values_from_configuration()
    {
        // Create the configuration
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                Frontends = new Dictionary<string, BffFrontendConfiguration>()
                {
                    [The.FrontendName] = new()
                    {
                        IndexHtmlUrl = The.Url,
                        MatchingOrigin = The.Origin.ToString(),
                        MatchingPath = The.Path,
                        Oidc = Some.OidcConfiguration(),
                        Cookies = Some.CookieConfiguration()
                        // ev: todo
                        //CallbackPath = The.Path

                    },

                }
            })
            .Build();


        // Wire up the BFF
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, FakeHttpContextAccessor>(); // We need the http context to set the scope
        services.AddBff().LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(1);

        var found = frontends.First(x => x.Name == The.FrontendName);
        found.ShouldBe(new BffFrontend
        {
            Name = The.FrontendName,
            IndexHtmlUrl = The.Url,
            SelectionCriteria = Some.FrontendSelectionCriteria()
        });

        var openIdConnectOptions = new OpenIdConnectOptions();
        found.ConfigureOpenIdConnectOptions!(openIdConnectOptions);
        ValidateOpenIdConnectOptions(openIdConnectOptions, The.CallbackPath.ToString());

        provider.GetRequiredService<SelectedFrontend>().Set(found);

        var factory = provider.GetRequiredService<IOptionsFactory<CookieAuthenticationOptions>>();
        var options = factory.Create(found.CookieSchemeName);

        ValidateCookieOptions(options);
    }


    [Fact]
    public void when_loading_frontend_default_config_is_applied()
    {
        // Create the configuration
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                DefaultOidcSettings = Some.OidcConfiguration(),
                DefaultCookieSettings = Some.CookieConfiguration(),
                Frontends = new Dictionary<string, BffFrontendConfiguration>()
                {
                    [The.FrontendName] = new()
                    {
                    },

                }
            })
            .Build();


        // Wire up the BFF
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, FakeHttpContextAccessor>(); // We need the http context to set the scope
        services.AddBff().LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(1);

        var found = frontends.First(x => x.Name == The.FrontendName);

        var oidcOptionsFactory = provider.GetRequiredService<IOptionsFactory<OpenIdConnectOptions>>();
        var openIdConnectOptions = oidcOptionsFactory.Create(frontends.First().OidcSchemeName);

        ValidateOpenIdConnectOptions(openIdConnectOptions, The.CallbackPath.ToString());

        provider.GetRequiredService<SelectedFrontend>().Set(found);

        var cookieOptionsFactory = provider.GetRequiredService<IOptionsFactory<CookieAuthenticationOptions>>();
        var cookieOptions = cookieOptionsFactory.Create(found.CookieSchemeName);

        ValidateCookieOptions(cookieOptions);
    }

    private void ValidateOpenIdConnectOptions(OpenIdConnectOptions options, PathString callbackPath)
    {
        options.ClientId.ShouldBe(The.ClientId);
        options.Authority.ShouldBe(The.Authority.ToString());
        options.ClientSecret.ShouldBe(The.ClientSecret);
        options.ResponseType.ShouldBe(The.ResponseType);
        options.CallbackPath.ShouldBe(callbackPath);
        options.ClientId.ShouldBe(The.ClientId);
    }

    [Fact]
    public void Can_configure_frontends_in_configuration_as_string()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()
            {

                ["frontends:_FrontendName_:matchingPath"] = The.Path,
                ["frontends:_FrontendName_:matchingOrigin"] = The.Origin.ToString(),
                ["frontends:_FrontendName_:indexHtmlUrl"] = The.Url.ToString(),
                ["frontends:_FrontendName_:Oidc:scope:0"] = The.Scope,
                ["frontends:_FrontendName_:Oidc:saveTokens"] = "True",
                ["frontends:_FrontendName_:Oidc:responseType"] = The.ResponseType,
                ["frontends:_FrontendName_:Oidc:responseMode"] = The.ResponseMode,
                ["frontends:_FrontendName_:Oidc:getClaimsFromUserInfoEndpoint"] = "True",
                ["frontends:_FrontendName_:Oidc:clientSecret"] = The.ClientSecret,
                ["frontends:_FrontendName_:Oidc:clientId"] = The.ClientId,
                ["frontends:_FrontendName_:Oidc:callbackPath"] = "", // ev: todo
                ["frontends:_FrontendName_:Oidc:authority"] = The.Authority.ToString(),
                ["frontends:_FrontendName_:Oidc:mapInboundClaims"] = "False",
                ["frontends:_FrontendName_:RemoteApis:0:localPath"] = The.Path,
                ["frontends:_FrontendName_:RemoteApis:0:targetUri"] = The.Url.ToString(),
                ["frontends:_FrontendName_:RemoteApis:0:requiredTokenType"] = The.RequiredTokenType.ToString(),
                ["frontends:_FrontendName_:RemoteApis:0:tokenRetrieverTypeName"] = The.TokenRetrieverType.AssemblyQualifiedName,
                ["frontends:_FrontendName_:RemoteApis:0:userAccessTokenParameters:signinScheme"] = The.Scheme,
                ["frontends:_FrontendName_:RemoteApis:0:userAccessTokenParameters:challengeScheme"] = The.Scheme,
                ["frontends:_FrontendName_:RemoteApis:0:userAccessTokenParameters:forceRenewal"] = true.ToString(),
                ["frontends:_FrontendName_:RemoteApis:0:userAccessTokenParameters:resource"] = The.Resource,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBff()
            .LoadConfiguration(configuration)
            .AddRemoteApis();
        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(1);

        var found = frontends.First(x => x.Name == The.FrontendName);
        var expected = new BffFrontend
        {
            Name = The.FrontendName,
            IndexHtmlUrl = The.Url,
            SelectionCriteria = Some.FrontendSelectionCriteria(),
            DataExtensions = found.DataExtensions
        };
        found.ShouldBe(expected);

        var openIdConnectOptions = new OpenIdConnectOptions();
        found.ConfigureOpenIdConnectOptions!(openIdConnectOptions);
        openIdConnectOptions.ShouldBeEquivalentTo(The.OpenIdConnectOptions);

        expected.DataExtensions.Length.ShouldBe(1);
        var proxyConfig = (ProxyBffPlugin)expected.DataExtensions[0];
        proxyConfig.RemoteApis.Length.ShouldBe(1);
        proxyConfig.RemoteApis[0].ShouldBe(Some.RemoteApi());
    }

    [Fact]
    public void When_config_file_is_updated_then_frontend_store_is_updated()
    {
        using var configFile = new ConfigFile();

        configFile.Save(new BffConfiguration()
        {
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                ["to_be_removed"] = new(),
                ["to_be_updated"] = new(),
            }
        });

        var services = new ServiceCollection();
        services.AddBff().LoadConfiguration(configFile.Configuration);
        var provider = services.BuildServiceProvider();
        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(2);

        configFile.Save(new BffConfiguration()
        {
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                ["to_be_added"] = new(),
                ["to_be_updated"] = new(),
            }
        });

        frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(2);

        var found = frontends.ToArray();
        found.ShouldBe([
            new BffFrontend
            {
                Name = BffFrontendName.Parse("to_be_added")
            },
            new BffFrontend()
            {
                Name = BffFrontendName.Parse("to_be_updated")
            }
        ]);
    }


    [Fact]
    public void When_updating_frontends_in_config_then_are_removed_from_Oidc_cache()
    {
        using var configFile = new ConfigFile();

        configFile.Save(new BffConfiguration()
        {
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                ["to_be_removed"] = new(),
                ["to_be_updated"] = new(),
            }
        });

        var services = new ServiceCollection();
        services.AddBff().LoadConfiguration(configFile.Configuration);
        var provider = services.BuildServiceProvider();
        var optionsCache = provider.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();

        optionsCache.TryAdd("to_be_removed", new OpenIdConnectOptions());
        optionsCache.TryAdd("to_be_updated", new OpenIdConnectOptions());

        var frontends = provider.GetRequiredService<FrontendCollection>().GetAll();

        configFile.Save(new BffConfiguration()
        {
            Frontends = new Dictionary<string, BffFrontendConfiguration>()
            {
                ["to_be_added"] = new(),
                ["to_be_updated"] = new(),
            }
        });

        frontends = provider.GetRequiredService<FrontendCollection>().GetAll();
        frontends.Count.ShouldBe(2);

        optionsCache.TryRemove("to_be_removed")
            .ShouldBeTrue("The frontend 'to_be_removed' is no longer in the config and should be removed from oidc config");
        optionsCache.TryRemove("to_be_updated")
            .ShouldBeTrue("The frontend 'to_be_updated' is changed. We need to clear it from the oidc cache.");

        optionsCache.TryRemove("to_be_added")
            .ShouldBeFalse("the frontend 'to_be_added' hasn't yet been added to the cache. ");
    }


    [Fact]
    public void Can_load_default_oidc_configuration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                DefaultOidcSettings = Some.OidcConfiguration()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, FakeHttpContextAccessor>();
        services.AddBff().LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OpenIdConnectOptions>>();

        ValidateOpenIdConnectOptions(options.Value, The.CallbackPath.ToString());

    }

    [Fact]
    public void Can_load_default_cookie_configuration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                DefaultCookieSettings = Some.CookieConfiguration()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, FakeHttpContextAccessor>();
        services.AddBff().LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IOptionsFactory<CookieAuthenticationOptions>>();
        var options = factory.Create(BffAuthenticationSchemes.BffCookie);

        ValidateCookieOptions(options);
    }

    private void ValidateCookieOptions(CookieAuthenticationOptions options)
    {
        options.Cookie.Name.ShouldBe(The.CookieName);
        options.Cookie.HttpOnly.ShouldBe(true);
        options.Cookie.Path.ShouldBe(The.Path);
        options.Cookie.SameSite.ShouldBe(SameSiteMode.Strict);
        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
        options.Cookie.Domain.ShouldBe(The.DomainName.Host);
        options.Cookie.MaxAge.ShouldBe(The.MaxAge);
    }

    [Fact(Skip = "should research other way of handling this")]
    public void Will_reject_invalid_configuration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                Frontends = new Dictionary<string, BffFrontendConfiguration>()
                {
                    [The.FrontendName] = Some.BffFrontendConfiguration() with
                    {
                        Oidc = new OidcConfiguration()
                        {
                            // When given a client id, the client secret must also be given
                            ClientId = "not-null",
                            ClientSecret = null
                        }
                    }
                    ,
                }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBff().LoadConfiguration(configuration);
        var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<BffConfiguration>>().Value);
    }

    [Fact]
    public void Cannot_add_duplicate_frontends()
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.AddBff()
            .AddFrontends(Some.BffFrontend(), Some.BffFrontend()))
            ;
    }
}

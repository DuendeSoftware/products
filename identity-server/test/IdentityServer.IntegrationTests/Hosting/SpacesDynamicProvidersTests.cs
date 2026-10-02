// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using System.Xml.Linq;
using Duende.IdentityModel;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.Spaces;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesDynamicProvidersTests(WebServerFixture webServerFixture) : IAsyncLifetime
{
    private const string OidcScheme = "my-oidc";
    private const string SamlScheme = "my-saml";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class Fixture : IAsyncDisposable
    {
        public required KestrelBasedTestServer MainServer { get; init; }
        public required KestrelBasedTestServer IdpServer { get; init; }

        public async ValueTask DisposeAsync()
        {
            await MainServer.DisposeAsync();
            await IdpServer.DisposeAsync();
        }
    }

    private sealed class MutableClientIdHolder
    {
        public string ClientId { get; set; } = "static-client-v1";
    }

    private Task<Fixture> CreateFixtureAsync(string name) =>
        CreateFixtureAsync(name,
            builder =>
            {
                builder.AddInMemoryCaching();
                builder.AddIdentityProviderStoreCache<IdentityProviderStore>();
            });

    private Task<Fixture> CreateFixtureAsync(string name, Action<IIdentityServerBuilder> configureIdentityProviderStore) =>
        CreateFixtureAsync(name, configureIdentityProviderStore, options => options.KeyManagement.Enabled = false);

    private Task<Fixture> CreateFixtureAsync(
        string name,
        Action<IIdentityServerBuilder> configureIdentityProviderStore,
        Action<IdentityServerOptions> configureOptions) =>
        CreateFixtureAsync(name, configureIdentityProviderStore, configureOptions, _ => { });

    private async Task<Fixture> CreateFixtureAsync(
        string name,
        Action<IIdentityServerBuilder> configureIdentityProviderStore,
        Action<IdentityServerOptions> configureOptions,
        Action<IServiceCollection> configureServices)
    {
        var dbName = $"msdynprov_{name}_{Guid.NewGuid():N}";
        var output = TestContext.Current.TestOutputHelper!;

        var mainHostAlias = $"ms{Guid.NewGuid():N}"[..10];
        var idpHostAlias = $"idp{Guid.NewGuid():N}"[..10];

        KestrelBasedTestServer? idpServer = null;

        var mainServer = new KestrelBasedTestServer(
            mainHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-main"),
            services =>
            {
                services.AddRouting();
                services.AddSpaces();

                var builder = services.AddIdentityServer(configureOptions)
                    .AddDeveloperSigningCredential(persistKey: false)
                    .AddSamlDynamicProvider();

                // AddSpaces() requires the storage infrastructure even when the identity provider
                // store itself is not Duende.Storage-backed.
                builder.AddStorage(storage => storage.AddSqliteInMemory(dbName))
                    .AddConfigurationStorage()
                    .AddOperationalStorage();

                configureIdentityProviderStore(builder);

                services.ConfigureAll<OpenIdConnectOptions>(options =>
                {
                    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                });

                configureServices(services);
            },
            webapp =>
            {
                webapp.UseSpaceResolution();
                webapp.UseIdentityServer();

                // Terminal test-only endpoints, implemented as plain middleware since endpoint
                // routing runs before space path rewrites are visible.
                webapp.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value == "/test/challenge")
                    {
                        await ctx.ChallengeAsync(OidcScheme,
                            new AuthenticationProperties { RedirectUri = ctx.Request.PathBase + "/test/finish" });
                        return;
                    }

                    if (ctx.Request.Path.Value == "/test/saml-challenge")
                    {
                        await ctx.ChallengeAsync(SamlScheme,
                            new AuthenticationProperties { RedirectUri = ctx.Request.PathBase + "/test/finish" });
                        return;
                    }

                    await next(ctx);
                });
            });

        idpServer = new KestrelBasedTestServer(
            idpHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-idp"),
            services =>
            {
                services.AddRouting();
                services.AddAuthorization();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([])
                    .AddInMemoryIdentityResources([])
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp => webapp.UseIdentityServer());

        await idpServer.StartAsync();
        await mainServer.StartAsync();

        var schema = mainServer.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        return new Fixture { MainServer = mainServer, IdpServer = idpServer };
    }

    private async Task<Fixture> CreateStorageFixtureAsync(string name) =>
        await CreateFixtureAsync(name, _ => { });

    private async Task<Fixture> CreateFallbackToDefaultFixtureAsync(string name) =>
        await CreateFixtureAsync(
            name,
            _ => { },
            options => options.KeyManagement.Enabled = false,
            services => services.Configure<SpacesOptions>(o => o.FallbackToDefault = true));

    private async Task<Fixture> CreateNonCachingWrapperFixtureAsync(string name) =>
        await CreateFixtureAsync(name,
            builder => builder.AddIdentityProviderStore<IdentityProviderStore>());

    private async Task<Fixture> CreateCachingWrapperFixtureAsync(string name, TimeSpan cacheDuration) =>
        await CreateFixtureAsync(
            name,
            builder =>
            {
                builder.AddInMemoryCaching();
                builder.AddIdentityProviderStoreCache<IdentityProviderStore>();
            },
            options =>
            {
                options.KeyManagement.Enabled = false;
                options.Caching.IdentityProviderCacheDuration = cacheDuration;
            });

    private async Task<(Fixture Fixture, MutableClientIdHolder Holder)> CreateStaticSchemeFixtureAsync(string name, string staticScheme)
    {
        var dbName = $"msdynprov_static_{name}_{Guid.NewGuid():N}";
        var output = TestContext.Current.TestOutputHelper!;

        var mainHostAlias = $"ms{Guid.NewGuid():N}"[..10];
        var idpHostAlias = $"idp{Guid.NewGuid():N}"[..10];

        var holder = new MutableClientIdHolder();

        KestrelBasedTestServer? idpServer = null;

        var mainServer = new KestrelBasedTestServer(
            mainHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-main"),
            services =>
            {
                services.AddRouting();
                services.AddSpaces();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddDeveloperSigningCredential(persistKey: false)
                    .AddStorage(storage => storage.AddSqliteInMemory(dbName))
                    .AddConfigurationStorage()
                    .AddOperationalStorage();

                services.AddAuthentication()
                    .AddOpenIdConnect(staticScheme, options =>
                    {
                        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                        options.Authority = idpServer!.BaseAddress.ToString().TrimEnd('/');
                        options.ClientId = holder.ClientId;
                        options.ResponseType = "code";
                        options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                    });
            },
            webapp =>
            {
                webapp.UseSpaceResolution();
                webapp.UseIdentityServer();

                webapp.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value == "/test/static-challenge")
                    {
                        await ctx.ChallengeAsync(staticScheme,
                            new AuthenticationProperties { RedirectUri = ctx.Request.PathBase + "/test/finish" });
                        return;
                    }

                    if (ctx.Request.Path.Value == "/test/reload-static")
                    {
                        // Stands in for an application calling IOptionsMonitorCache.TryRemove to
                        // force a reload of a static scheme's options (for example, after a
                        // secret rotation), independent of any space.
                        var cache = ctx.RequestServices.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();
                        cache.TryRemove(staticScheme);
                        ctx.Response.StatusCode = (int)HttpStatusCode.NoContent;
                        return;
                    }

                    await next(ctx);
                });
            });

        idpServer = new KestrelBasedTestServer(
            idpHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-idp"),
            services =>
            {
                services.AddRouting();
                services.AddAuthorization();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([])
                    .AddInMemoryIdentityResources([])
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp => webapp.UseIdentityServer());

        await idpServer.StartAsync();
        await mainServer.StartAsync();

        var schema = mainServer.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        return (new Fixture { MainServer = mainServer, IdpServer = idpServer }, holder);
    }

    private async Task<(Fixture Fixture, SpaceId SpaceAId, SpaceId SpaceBId)> CreateFixtureWithTwoSpacesAsync(
        Func<string, Task<Fixture>> createFixture, string name)
    {
        var fixture = await createFixture(name);

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();

        var createSpaceAResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] },
            _ct);
        createSpaceAResult.IsSuccess.ShouldBeTrue($"Create space A failed: {createSpaceAResult}");

        var createSpaceBResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] },
            _ct);
        createSpaceBResult.IsSuccess.ShouldBeTrue($"Create space B failed: {createSpaceBResult}");

        return (fixture, createSpaceAResult.Id!, createSpaceBResult.Id!);
    }

    // Full external-login round trip through a dynamic OIDC provider, and client
    // IdentityProviderRestrictions filtering against the resulting (bare) idp claim.
    private async Task<Fixture> CreateExternalLoginFixtureAsync(string name, string urlPrefix)
    {
        var dbName = $"msextlogin_{name}_{Guid.NewGuid():N}";
        var output = TestContext.Current.TestOutputHelper!;

        var mainHostAlias = $"ms{Guid.NewGuid():N}"[..10];
        var idpHostAlias = $"idp{Guid.NewGuid():N}"[..10];

        KestrelBasedTestServer? idpServer = null;

        var mainServer = new KestrelBasedTestServer(
            mainHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-main"),
            services =>
            {
                services.AddRouting();
                services.AddSpaces();

                var builder = services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddDeveloperSigningCredential(persistKey: false)
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = "allowed-client",
                            AllowedGrantTypes = GrantTypes.Code,
                            RequireClientSecret = false,
                            RequirePkce = true,
                            RequireConsent = false,
                            RedirectUris = { "https://rp.example/callback" },
                            AllowedScopes = { "openid" },
                            IdentityProviderRestrictions = { OidcScheme }
                        },
                        new Client
                        {
                            ClientId = "restricted-client",
                            AllowedGrantTypes = GrantTypes.Code,
                            RequireClientSecret = false,
                            RequirePkce = true,
                            RequireConsent = false,
                            RedirectUris = { "https://rp.example/callback" },
                            AllowedScopes = { "openid" },
                            IdentityProviderRestrictions = { "some-other-idp" }
                        }
                    ])
                    .AddInMemoryIdentityResources([new IdentityResources.OpenId()]);

                builder.AddStorage(storage => storage.AddSqliteInMemory(dbName))
                    .AddConfigurationStorage()
                    .AddOperationalStorage();

                services.ConfigureAll<OpenIdConnectOptions>(options =>
                {
                    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                });
            },
            webapp =>
            {
                webapp.UseSpaceResolution();
                webapp.UseIdentityServer();

                webapp.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value == "/test/challenge")
                    {
                        // Mirrors the Quickstart UI's ExternalLogin/Challenge.cshtml.cs pattern:
                        // it records the (bare) scheme name in properties so the callback page
                        // can build the idp claim from it explicitly, rather than relying on the
                        // amr-to-idp claim conversion (which targets ASP.NET Identity's own
                        // external-login pipeline).
                        await ctx.ChallengeAsync(OidcScheme,
                            new AuthenticationProperties
                            {
                                RedirectUri = ctx.Request.PathBase + "/test/finish",
                                Items = { ["scheme"] = OidcScheme }
                            });
                        return;
                    }

                    if (ctx.Request.Path.Value == "/test/finish")
                    {
                        var external = await ctx.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
                        if (external.Succeeded)
                        {
                            var provider = external.Properties?.Items["scheme"] ?? "unknown identity provider";
                            var externalClaims = external.Principal!.Claims
                                .Where(c => c.Type is not JwtClaimTypes.IdentityProvider);
                            var localIdentity = new ClaimsIdentity(
                                externalClaims.Append(new Claim(JwtClaimTypes.IdentityProvider, provider)),
                                IdentityServerConstants.DefaultCookieAuthenticationScheme);
                            var localPrincipal = new ClaimsPrincipal(localIdentity);

                            await ctx.SignInAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme, localPrincipal, external.Properties);
                            await ctx.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

                            ctx.Response.StatusCode = 200;
                            await ctx.Response.WriteAsync(provider);
                        }
                        else
                        {
                            ctx.Response.StatusCode = 401;
                        }

                        return;
                    }

                    await next(ctx);
                });
            });

        idpServer = new KestrelBasedTestServer(
            idpHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-idp"),
            services =>
            {
                services.AddRouting();
                services.AddAuthorization();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = "client",
                            AllowedGrantTypes = GrantTypes.Code,
                            RequireClientSecret = false,
                            RequirePkce = true,
                            RequireConsent = false,
                            RedirectUris = { mainServer.BuildUrl($"{urlPrefix}/federation/{OidcScheme}/signin").ToString() },
                            AllowedScopes = { "openid" }
                        }
                    ])
                    .AddInMemoryIdentityResources([new IdentityResources.OpenId()])
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp =>
            {
                webapp.UseIdentityServer();

                webapp.MapGet("/login", async ctx =>
                {
                    var sub = ctx.Request.Query["sub"].FirstOrDefault() ?? "external-user";
                    await ctx.SignInAsync(new IdentityServerUser(sub).CreatePrincipal());
                });
            });

        await idpServer.StartAsync();
        await mainServer.StartAsync();

        var schema = mainServer.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        return new Fixture { MainServer = mainServer, IdpServer = idpServer };
    }

    private async Task<IdentityProviderId> CreateOidcProviderInSpaceAsync(KestrelBasedTestServer mainServer, SpaceId spaceId, string scheme, string authority, string clientId)
    {
        using var scope = mainServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var config = new CreateIdentityProvider
            {
                Scheme = scheme,
                Type = "oidc"
            };
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.Authority)), authority);
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.ClientId)), clientId);

            var result = await admin.CreateAsync(config, _ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for space {spaceId}: {result}");
            return result.Id!;
        }
    }

    // Registers a dynamic OIDC provider configured for a real authorization_code + PKCE login
    // round trip (ResponseType "code", no client secret, matching the public client registered
    // at the external idp), unlike CreateOidcProviderInSpaceAsync above, whose providers are
    // only ever used to inspect challenge redirects.
    private async Task CreateOidcProviderForLoginInSpaceAsync(KestrelBasedTestServer mainServer, SpaceId spaceId, string scheme, string authority, string clientId)
    {
        using var scope = mainServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var config = new CreateIdentityProvider { Scheme = scheme, Type = "oidc" };
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.Authority)), authority);
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.ClientId)), clientId);
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.ResponseType)), "code");

            var result = await admin.CreateAsync(config, _ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for space {spaceId}: {result}");
        }
    }

    private async Task DeleteOidcProviderInSpaceAsync(KestrelBasedTestServer mainServer, SpaceId spaceId, IdentityProviderId providerId)
    {
        using var scope = mainServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var deleteResult = await admin.DeleteAsync(providerId, _ct);
            deleteResult.IsSuccess.ShouldBeTrue($"Delete failed for space {spaceId}: {deleteResult}");
        }
    }

    private async Task UpdateOidcProviderClientIdInSpaceAsync(KestrelBasedTestServer mainServer, SpaceId spaceId, IdentityProviderId providerId, string newClientId)
    {
        using var scope = mainServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var getResult = await admin.GetAsync(providerId, _ct);
            getResult.Found.ShouldBeTrue($"Provider {providerId} not found in space {spaceId}");

            var toUpdate = getResult.Item.ToUpdate();
            toUpdate.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.ClientId)), newClientId);

            var updateResult = await admin.UpdateAsync(providerId, toUpdate, getResult.Version!, _ct);
            updateResult.IsSuccess.ShouldBeTrue($"Update failed for space {spaceId}: {updateResult}");
        }
    }

    private async Task CreateSamlProviderInSpaceAsync(KestrelBasedTestServer mainServer, SpaceId spaceId, string ssoUrl, string spEntityId)
    {
        using var scope = mainServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var config = new CreateIdentityProvider { Scheme = SamlScheme, Type = "saml" };
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.IdpEntityId)), $"{ssoUrl}-idp-entity");
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SingleSignOnServiceUrl)), ssoUrl);
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SpEntityId)), spEntityId);

            var result = await admin.CreateAsync(config, _ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for space {spaceId}: {result}");
        }
    }

    private async Task RunUpdateInSpaceADoesNotAffectSpaceBAsync(Func<string, Task<Fixture>> createFixture, string name)
    {
        var (fixture, spaceAId, spaceBId) = await CreateFixtureWithTwoSpacesAsync(createFixture, name);
        await using var _ = fixture;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        var providerAId = await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a-v1");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceBId, OidcScheme, idpAuthority, "client-b");

        using var mainClient = fixture.MainServer.CreateClient();

        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/challenge", _ct)).ShouldBe("client-a-v1");
        ExtractClientId(await mainClient.GetAsync("/t/space-b/test/challenge", _ct)).ShouldBe("client-b");

        await UpdateOidcProviderClientIdInSpaceAsync(fixture.MainServer, spaceAId, providerAId, "client-a-v2");

        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/challenge", _ct)).ShouldBe("client-a-v2",
            "space A must observe its own configuration update");

        ExtractClientId(await mainClient.GetAsync("/t/space-b/test/challenge", _ct)).ShouldBe("client-b",
            "space B must be unaffected by space A's configuration update");
    }

    private static async Task<HttpResponseMessage> RunExternalLoginAsync(HttpClient mainClient, HttpClient idpClient, string urlPrefix, string sub)
    {
        var challenge = await mainClient.GetAsync($"{urlPrefix}/test/challenge");
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var authorizeUrl = challenge.Headers.Location!.ToString();

        (await idpClient.GetAsync($"/login?sub={sub}")).EnsureSuccessStatusCode();
        var idpAuthorize = await idpClient.GetAsync(authorizeUrl);
        idpAuthorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        var redirectUri = idpAuthorize.Headers.Location!.ToString();

        var callback = await mainClient.GetAsync(redirectUri);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finishUri = callback.Headers.Location!.ToString();

        return await mainClient.GetAsync(finishUri);
    }

    private static string ExtractClientId(HttpResponseMessage challengeResponse)
    {
        challengeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var authorizeUrl = challengeResponse.Headers.Location!.ToString();
        var query = HttpUtility.ParseQueryString(new Uri(authorizeUrl).Query);
        return query["client_id"]!;
    }

    private static string ExtractRedirectUri(HttpResponseMessage challengeResponse)
    {
        challengeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var authorizeUrl = challengeResponse.Headers.Location!.ToString();
        var query = HttpUtility.ParseQueryString(new Uri(authorizeUrl).Query);
        return query["redirect_uri"]!;
    }

    private static string ExtractSamlAuthnRequestIssuer(HttpResponseMessage response)
    {
        response.Headers.Location.ShouldNotBeNull();
        var query = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        var encoded = query["SAMLRequest"];
        encoded.ShouldNotBeNullOrWhiteSpace("SAMLRequest not found in redirect query string");

        var compressedBytes = Convert.FromBase64String(encoded!);
        using var inputStream = new MemoryStream(compressedBytes);
        using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        deflateStream.CopyTo(outputStream);
        var requestXml = Encoding.UTF8.GetString(outputStream.ToArray());

        var doc = XDocument.Parse(requestXml);
        var issuer = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Issuer")?.Value;
        issuer.ShouldNotBeNullOrWhiteSpace("AuthnRequest must contain an Issuer element");
        return issuer!;
    }

    [Fact]
    public async Task challenge_in_space_should_use_that_spaces_provider_when_store_is_cached()
    {
        await using var fixture = await CreateFixtureAsync(
            nameof(challenge_in_space_should_use_that_spaces_provider_when_store_is_cached));

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();
        var createSpaceResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue($"Create space failed: {createSpaceResult}");
        var spaceAId = createSpaceResult.Id!;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        // Seed the same scheme name in the Default space; if the caching store's factory loses
        // the caller's space context, it falls back to Default and returns this provider instead.
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, SpaceId.Default, OidcScheme, idpAuthority, "client-default");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a");

        using var mainClient = fixture.MainServer.CreateClient();

        var challenge = await mainClient.GetAsync("/t/space-a/test/challenge", _ct);

        var clientId = ExtractClientId(challenge);
        clientId.ShouldBe("client-a");
    }

    [Fact]
    public async Task GetAllSchemeNamesAsync_in_space_should_return_only_that_spaces_providers_when_store_is_cached()
    {
        await using var fixture = await CreateFixtureAsync(
            nameof(GetAllSchemeNamesAsync_in_space_should_return_only_that_spaces_providers_when_store_is_cached));

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();
        var createSpaceResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue($"Create space failed: {createSpaceResult}");
        var spaceAId = createSpaceResult.Id!;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        // Seed distinct scheme names per space so a Default-space fallback is observable.
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, SpaceId.Default, "default-oidc", idpAuthority, "client-default");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, "space-a-oidc", idpAuthority, "client-a");

        using var scope = fixture.MainServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var store = scope.ServiceProvider.GetRequiredService<IIdentityProviderStore>();

        using (accessor.SetSpace(spaceAId))
        {
            var names = await store.GetAllSchemeNamesAsync(_ct);
            names.Select(n => n.Scheme).ShouldBe(["space-a-oidc"]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task challenge_in_space_should_not_leak_other_spaces_provider_when_store_is_storage_without_wrapper(bool spaceAFirst)
    {
        var (fixture, spaceAId, spaceBId) = await CreateFixtureWithTwoSpacesAsync(CreateStorageFixtureAsync,
            nameof(challenge_in_space_should_not_leak_other_spaces_provider_when_store_is_storage_without_wrapper) + spaceAFirst);
        await using var _ = fixture;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceBId, OidcScheme, idpAuthority, "client-b");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a");

        using var mainClient = fixture.MainServer.CreateClient();

        if (spaceAFirst)
        {
            var firstChallenge = await mainClient.GetAsync("/t/space-a/test/challenge", _ct);
            ExtractClientId(firstChallenge).ShouldBe("client-a");

            var spaceBChallenge = await mainClient.GetAsync("/t/space-b/test/challenge", _ct);
            ExtractClientId(spaceBChallenge).ShouldBe("client-b",
                "space B must not receive space A's leaked provider");
        }
        else
        {
            var spaceBChallenge = await mainClient.GetAsync("/t/space-b/test/challenge", _ct);
            ExtractClientId(spaceBChallenge).ShouldBe("client-b");

            var spaceAChallenge = await mainClient.GetAsync("/t/space-a/test/challenge", _ct);
            ExtractClientId(spaceAChallenge).ShouldBe("client-a",
                "space A must not receive space B's leaked provider");
        }
    }

    [Fact]
    public async Task saml_challenge_in_space_should_not_leak_other_spaces_provider()
    {
        var (fixture, spaceAId, spaceBId) = await CreateFixtureWithTwoSpacesAsync(CreateStorageFixtureAsync,
            nameof(saml_challenge_in_space_should_not_leak_other_spaces_provider));
        await using var _ = fixture;

        const string spaceBSsoUrl = "https://space-b-idp.example/sso";
        const string spaceASsoUrl = "https://space-a-idp.example/sso";

        await CreateSamlProviderInSpaceAsync(fixture.MainServer, spaceBId, spaceBSsoUrl, "space-b-sp-entity");
        await CreateSamlProviderInSpaceAsync(fixture.MainServer, spaceAId, spaceASsoUrl, "space-a-sp-entity");

        using var mainClient = fixture.MainServer.CreateClient();

        var spaceBChallenge = await mainClient.GetAsync("/t/space-b/test/saml-challenge", _ct);
        spaceBChallenge.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        spaceBChallenge.Headers.Location!.ToString().ShouldStartWith(spaceBSsoUrl, Case.Sensitive);
        ExtractSamlAuthnRequestIssuer(spaceBChallenge).ShouldBe("space-b-sp-entity");

        var spaceAChallenge = await mainClient.GetAsync("/t/space-a/test/saml-challenge", _ct);
        spaceAChallenge.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        spaceAChallenge.Headers.Location!.ToString().ShouldStartWith(spaceASsoUrl, Case.Sensitive,
            "space A must not receive space B's leaked SingleSignOnServiceUrl");
        ExtractSamlAuthnRequestIssuer(spaceAChallenge).ShouldBe("space-a-sp-entity",
            "space A must not receive space B's leaked SpEntityId");
    }

    [Fact]
    public async Task deleting_provider_in_space_should_evict_that_spaces_cached_options()
    {
        var (fixture, spaceAId, _) = await CreateFixtureWithTwoSpacesAsync(CreateStorageFixtureAsync,
            nameof(deleting_provider_in_space_should_evict_that_spaces_cached_options));
        await using var _ = fixture;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        var providerId = await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a");

        using var mainClient = fixture.MainServer.CreateClient();

        var spaceAChallenge = await mainClient.GetAsync("/t/space-a/test/challenge", _ct);
        ExtractClientId(spaceAChallenge).ShouldBe("client-a");

        await DeleteOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, providerId);

        // Cached options must not outlive the deleted provider; they hold secrets.
        var challengeAfterDelete = await mainClient.GetAsync("/t/space-a/test/challenge", _ct);
        var location = challengeAfterDelete.Headers.Location;
        var clientIdAfterDelete = location is { IsAbsoluteUri: true }
            ? HttpUtility.ParseQueryString(location.Query)["client_id"]
            : null;
        clientIdAfterDelete.ShouldNotBe("client-a");
    }

    [Fact]
    public async Task challenge_in_space_resolved_by_host_should_not_leak_other_spaces_provider()
    {
        var fixture = await CreateStorageFixtureAsync(
            nameof(challenge_in_space_resolved_by_host_should_not_leak_other_spaces_provider));
        await using var _ = fixture;

        var hostA = $"h{Guid.NewGuid():N}"[..10] + ".dev.localhost";
        var hostB = $"h{Guid.NewGuid():N}"[..10] + ".dev.localhost";
        fixture.MainServer.RegisterHostAlias(hostA);
        fixture.MainServer.RegisterHostAlias(hostB);

        var originA = new UriBuilder(fixture.MainServer.BaseAddress) { Host = hostA }.Uri;
        var originB = new UriBuilder(fixture.MainServer.BaseAddress) { Host = hostB }.Uri;

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();
        var createSpaceAResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Host Space A", MatchPatterns = [new SpaceMatchPattern { Origin = originA.GetLeftPart(UriPartial.Authority) }] },
            _ct);
        createSpaceAResult.IsSuccess.ShouldBeTrue($"Create space A failed: {createSpaceAResult}");
        var spaceAId = createSpaceAResult.Id!.Value;

        var createSpaceBResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Host Space B", MatchPatterns = [new SpaceMatchPattern { Origin = originB.GetLeftPart(UriPartial.Authority) }] },
            _ct);
        createSpaceBResult.IsSuccess.ShouldBeTrue($"Create space B failed: {createSpaceBResult}");
        var spaceBId = createSpaceBResult.Id!.Value;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceBId, OidcScheme, idpAuthority, "client-b");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a");

        using var mainClient = fixture.MainServer.CreateClient();

        var challengeA = await mainClient.GetAsync(new Uri(originA, "/test/challenge"), _ct);
        ExtractClientId(challengeA).ShouldBe("client-a");

        var challengeB = await mainClient.GetAsync(new Uri(originB, "/test/challenge"), _ct);
        ExtractClientId(challengeB).ShouldBe("client-b",
            "host-resolved space B must not receive space A's leaked provider");
    }

    [Fact]
    public async Task twenty_concurrent_alternating_challenges_should_never_mismatch()
    {
        var (fixture, spaceAId, spaceBId) = await CreateFixtureWithTwoSpacesAsync(CreateStorageFixtureAsync,
            nameof(twenty_concurrent_alternating_challenges_should_never_mismatch));
        await using var _ = fixture;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceBId, OidcScheme, idpAuthority, "client-b");

        using var mainClient = fixture.MainServer.CreateClient();

        var mismatches = new ConcurrentBag<string>();

        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            var (path, expectedClientId) = i % 2 == 0
                ? ("/t/space-a/test/challenge", "client-a")
                : ("/t/space-b/test/challenge", "client-b");

            var response = await mainClient.GetAsync(path, _ct);
            var actualClientId = ExtractClientId(response);
            if (actualClientId != expectedClientId)
            {
                mismatches.Add($"iteration {i} ({path}): expected {expectedClientId}, got {actualClientId}");
            }
        });

        await Task.WhenAll(tasks);

        mismatches.ShouldBeEmpty($"mismatches: {string.Join("; ", mismatches)}");
    }

    [Fact]
    public async Task update_in_space_a_does_not_affect_space_b_when_store_is_storage_without_wrapper() =>
        await RunUpdateInSpaceADoesNotAffectSpaceBAsync(CreateStorageFixtureAsync,
            nameof(update_in_space_a_does_not_affect_space_b_when_store_is_storage_without_wrapper));

    [Fact]
    public async Task update_in_space_a_does_not_affect_space_b_when_store_is_non_caching_wrapper() =>
        await RunUpdateInSpaceADoesNotAffectSpaceBAsync(CreateNonCachingWrapperFixtureAsync,
            nameof(update_in_space_a_does_not_affect_space_b_when_store_is_non_caching_wrapper));

    [Fact]
    public async Task update_in_space_a_does_not_affect_space_b_when_store_is_caching_wrapper()
    {
        var cacheDuration = TimeSpan.FromMilliseconds(200);
        var (fixture, spaceAId, spaceBId) = await CreateFixtureWithTwoSpacesAsync(
            n => CreateCachingWrapperFixtureAsync(n, cacheDuration),
            nameof(update_in_space_a_does_not_affect_space_b_when_store_is_caching_wrapper));
        await using var _ = fixture;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        var providerAId = await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a-v1");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceBId, OidcScheme, idpAuthority, "client-b");

        using var mainClient = fixture.MainServer.CreateClient();

        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/challenge", _ct)).ShouldBe("client-a-v1");
        ExtractClientId(await mainClient.GetAsync("/t/space-b/test/challenge", _ct)).ShouldBe("client-b");

        await UpdateOidcProviderClientIdInSpaceAsync(fixture.MainServer, spaceAId, providerAId, "client-a-v2");

        // Wait for the caching store's model-level cache entry to expire.
        await Task.Delay(cacheDuration + TimeSpan.FromMilliseconds(300), _ct);

        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/challenge", _ct)).ShouldBe("client-a-v2",
            "space A must observe its own configuration update once the model cache entry expires");

        ExtractClientId(await mainClient.GetAsync("/t/space-b/test/challenge", _ct)).ShouldBe("client-b",
            "space B must be unaffected by space A's configuration update");
    }

    [Fact]
    public async Task static_scheme_stays_bare_and_framework_reload_evicts_in_both_spaces()
    {
        const string staticScheme = "static-oidc";
        var (fixture, holder) = await CreateStaticSchemeFixtureAsync(
            nameof(static_scheme_stays_bare_and_framework_reload_evicts_in_both_spaces), staticScheme);
        await using var _ = fixture;

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();
        var createSpaceAResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] },
            _ct);
        createSpaceAResult.IsSuccess.ShouldBeTrue($"Create space A failed: {createSpaceAResult}");

        var createSpaceBResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] },
            _ct);
        createSpaceBResult.IsSuccess.ShouldBeTrue($"Create space B failed: {createSpaceBResult}");

        using var mainClient = fixture.MainServer.CreateClient();

        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/static-challenge", _ct)).ShouldBe("static-client-v1");
        ExtractClientId(await mainClient.GetAsync("/t/space-b/test/static-challenge", _ct)).ShouldBe("static-client-v1",
            "a static scheme must stay bare/unqualified: both spaces share the same entry");

        holder.ClientId = "static-client-v2";

        // Not yet evicted: the framework's own reload has not run.
        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/static-challenge", _ct)).ShouldBe("static-client-v1");

        (await mainClient.GetAsync("/t/space-a/test/reload-static", _ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/static-challenge", _ct)).ShouldBe("static-client-v2");
        ExtractClientId(await mainClient.GetAsync("/t/space-b/test/static-challenge", _ct)).ShouldBe("static-client-v2",
            "a static scheme's options are never space-partitioned, so the reload is observed in both spaces");
    }

    [Fact]
    public async Task callback_urls_stay_bare_across_spaces()
    {
        var (fixture, spaceAId, spaceBId) = await CreateFixtureWithTwoSpacesAsync(CreateStorageFixtureAsync,
            nameof(callback_urls_stay_bare_across_spaces));
        await using var _ = fixture;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceBId, OidcScheme, idpAuthority, "client-b");

        using var mainClient = fixture.MainServer.CreateClient();

        var challengeA = await mainClient.GetAsync("/t/space-a/test/challenge", _ct);
        var redirectUriA = ExtractRedirectUri(challengeA);
        redirectUriA.ShouldContain($"/federation/{OidcScheme}/signin");
        redirectUriA.ShouldNotContain(spaceAId.Value.ToString());

        var challengeB = await mainClient.GetAsync("/t/space-b/test/challenge", _ct);
        var redirectUriB = ExtractRedirectUri(challengeB);
        redirectUriB.ShouldContain($"/federation/{OidcScheme}/signin");
        redirectUriB.ShouldNotContain(spaceBId.Value.ToString());
    }

    [Fact]
    public async Task explicitly_configured_default_space_is_its_own_partition()
    {
        var fixture = await CreateFallbackToDefaultFixtureAsync(
            nameof(explicitly_configured_default_space_is_its_own_partition));
        await using var _ = fixture;

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();
        var createSpaceAResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] },
            _ct);
        createSpaceAResult.IsSuccess.ShouldBeTrue($"Create space A failed: {createSpaceAResult}");
        var spaceAId = createSpaceAResult.Id!.Value;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');

        // A request to a path that matches no configured space, with FallbackToDefault enabled,
        // is resolved to an explicitly configured SpaceId.Default (SpaceResolutionMiddleware
        // calls accessor.SetSpace(SpaceId.Default), so IsSpaceIdConfigured() is true there too).
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, SpaceId.Default, OidcScheme, idpAuthority, "client-default");
        await CreateOidcProviderInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client-a");

        using var mainClient = fixture.MainServer.CreateClient();

        ExtractClientId(await mainClient.GetAsync("/test/challenge", _ct)).ShouldBe("client-default",
            "a path that falls back to the explicitly configured Default space must use its own provider");
        ExtractClientId(await mainClient.GetAsync("/t/space-a/test/challenge", _ct)).ShouldBe("client-a",
            "Space A must not receive the explicitly configured Default space's leaked provider");
        ExtractClientId(await mainClient.GetAsync("/test/challenge", _ct)).ShouldBe("client-default",
            "the explicitly configured Default space must not receive Space A's leaked provider");
    }

    [Fact]
    public async Task host_without_spaces_keeps_scheme_names_bare()
    {
        var output = TestContext.Current.TestOutputHelper!;
        var mainHostAlias = $"ms{Guid.NewGuid():N}"[..10];
        var idpHostAlias = $"idp{Guid.NewGuid():N}"[..10];

        KestrelBasedTestServer? idpServer = null;

        var mainServer = new KestrelBasedTestServer(
            mainHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, nameof(host_without_spaces_keeps_scheme_names_bare) + "-main"),
            services =>
            {
                services.AddRouting();
                // Deliberately no services.AddSpaces() call: ISpaceContextAccessor is not
                // registered at all, unlike the no-spaces regression host in DynamicProvidersTests
                // (which covers a *static* OIDC scheme end-to-end login instead).

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddDeveloperSigningCredential(persistKey: false)
                    .AddInMemoryOidcProviders([
                        new OidcProvider
                        {
                            Scheme = OidcScheme,
                            Authority = idpServer!.BaseAddress.ToString().TrimEnd('/'),
                            ClientId = "client-a"
                        }
                    ]);

                services.ConfigureAll<OpenIdConnectOptions>(options =>
                {
                    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                });
            },
            webapp =>
            {
                // Deliberately no webapp.UseSpaceResolution().
                webapp.UseIdentityServer();

                webapp.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value == "/test/challenge")
                    {
                        await ctx.ChallengeAsync(OidcScheme,
                            new AuthenticationProperties { RedirectUri = "/test/finish" });
                        return;
                    }

                    await next(ctx);
                });
            });

        idpServer = new KestrelBasedTestServer(
            idpHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, nameof(host_without_spaces_keeps_scheme_names_bare) + "-idp"),
            services =>
            {
                services.AddRouting();
                services.AddAuthorization();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([])
                    .AddInMemoryIdentityResources([])
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp => webapp.UseIdentityServer());

        await using var fixture = new Fixture { MainServer = mainServer, IdpServer = idpServer };

        await idpServer.StartAsync();
        await mainServer.StartAsync();

        using var mainClient = mainServer.CreateClient();
        var challenge = await mainClient.GetAsync("/test/challenge", _ct);
        var redirectUri = ExtractRedirectUri(challenge);

        redirectUri.ShouldContain($"/federation/{OidcScheme}/signin");
    }

    [Fact]
    public async Task external_login_through_dynamic_provider_in_a_space_produces_a_bare_idp_claim()
    {
        const string spacePath = "/space-a";
        const string urlPrefix = "/t" + spacePath;
        await using var fixture = await CreateExternalLoginFixtureAsync(
            nameof(external_login_through_dynamic_provider_in_a_space_produces_a_bare_idp_claim), urlPrefix);

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();
        var createSpaceResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = spacePath }] },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue($"Create space failed: {createSpaceResult}");
        var spaceAId = createSpaceResult.Id!.Value;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');
        await CreateOidcProviderForLoginInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client");

        using var mainClient = fixture.MainServer.CreateClient();
        using var idpClient = fixture.IdpServer.CreateClient();

        var finish = await RunExternalLoginAsync(mainClient, idpClient, urlPrefix, "alice");

        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        var idpClaim = await finish.Content.ReadAsStringAsync(_ct);
        idpClaim.ShouldBe(OidcScheme,
            "the idp claim produced by a full external login round trip through a dynamic " +
            "provider in a space must be the bare scheme name, never any internal space-partitioning key");
    }

    [Fact]
    public async Task identity_provider_restrictions_with_the_bare_scheme_name_accept_the_login_and_reject_others()
    {
        const string spacePath = "/space-a";
        const string urlPrefix = "/t" + spacePath;
        await using var fixture = await CreateExternalLoginFixtureAsync(
            nameof(identity_provider_restrictions_with_the_bare_scheme_name_accept_the_login_and_reject_others), urlPrefix);

        var spaceAdmin = fixture.MainServer.GetRequiredService<ISpaceAdmin>();
        var createSpaceResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = spacePath }] },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue($"Create space failed: {createSpaceResult}");
        var spaceAId = createSpaceResult.Id!.Value;

        var idpAuthority = fixture.IdpServer.BuildUrl("/").ToString().TrimEnd('/');
        await CreateOidcProviderForLoginInSpaceAsync(fixture.MainServer, spaceAId, OidcScheme, idpAuthority, "client");

        using var mainClient = fixture.MainServer.CreateClient();
        using var idpClient = fixture.IdpServer.CreateClient();

        var finish = await RunExternalLoginAsync(mainClient, idpClient, urlPrefix, "alice");
        finish.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The client's IdentityProviderRestrictions lists the bare scheme name ("my-oidc"),
        // matching the (bare) idp claim on the now-authenticated main session: the authorize
        // request must succeed silently (redirect straight to the client's redirect_uri).
        var allowedAuthorize = await mainClient.GetAsync(
            $"{urlPrefix}/connect/authorize?client_id=allowed-client&response_type=code&scope=openid" +
            "&redirect_uri=https%3A%2F%2Frp.example%2Fcallback&code_challenge=abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ" +
            "&code_challenge_method=S256&state=s1", _ct);
        allowedAuthorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther,
            "a client whose IdentityProviderRestrictions contains the bare idp claim value must authorize silently");
        allowedAuthorize.Headers.Location!.ToString().ShouldStartWith("https://rp.example/callback");
        HttpUtility.ParseQueryString(allowedAuthorize.Headers.Location!.Query)["code"].ShouldNotBeNullOrWhiteSpace();

        // The other client's IdentityProviderRestrictions does not include this idp: the
        // request must require login again (redirected to the login page), not authorize.
        var restrictedAuthorize = await mainClient.GetAsync(
            $"{urlPrefix}/connect/authorize?client_id=restricted-client&response_type=code&scope=openid" +
            "&redirect_uri=https%3A%2F%2Frp.example%2Fcallback&code_challenge=abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ" +
            "&code_challenge_method=S256&state=s2", _ct);
        restrictedAuthorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        restrictedAuthorize.Headers.Location!.ToString().ShouldNotStartWith("https://rp.example/callback");
        restrictedAuthorize.Headers.Location!.ToString().ShouldContain("/account/login");
    }
}


// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.Endpoints.Saml;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
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

namespace Duende.IdentityServer.IntegrationTests.Hosting;

// Shared fixture for the federation scenario tests: a spaces-enabled federation
// IdentityServer (acting as relying party) plus two independent remote IdPs. Space A's
// dynamic OIDC provider points at Idp1 and space B's at Idp2, under the same bare scheme
// name. Each remote IdP's /login endpoint ignores the query string and signs in a FIXED
// per-IdP subject, so the test driver cannot influence which identity is issued.
internal sealed class SpacesFederationTestHost : IAsyncDisposable
{
    public const string OidcScheme = "fed-oidc";
    public const string SamlScheme = "fed-saml";
    public const string Idp1Subject = "idp1-user";
    public const string Idp2Subject = "idp2-user";
    public const string SpaceBClientId = "space-b-client";

    public required KestrelBasedTestServer FederationServer { get; init; }
    public required KestrelBasedTestServer Idp1 { get; init; }
    public required KestrelBasedTestServer Idp2 { get; init; }
    public required ISpaceAdmin SpaceAdmin { get; init; }
    public required SpaceId SpaceAId { get; init; }
    public required SpaceId SpaceBId { get; init; }
    public required string HostSpaceAHostAlias { get; init; }
    public required string HostSpaceBHostAlias { get; init; }
    public required string Idp1SamlSigningCertificateBase64 { get; init; }
    public required string Idp2SamlSigningCertificateBase64 { get; init; }
    public required string SpSamlSigningCertificateBase64 { get; init; }

    public string Idp1Authority => Idp1.BuildUrl("/").ToString().TrimEnd('/');
    public string Idp2Authority => Idp2.BuildUrl("/").ToString().TrimEnd('/');
    public string Idp1SamlIssuer => $"{Idp1Authority}/Saml2";
    public string Idp2SamlIssuer => $"{Idp2Authority}/Saml2";

    private X509Certificate2? _idp1SamlSigningCert;
    private X509Certificate2? _idp2SamlSigningCert;
    private X509Certificate2? _spSamlSigningCert;

    public async ValueTask DisposeAsync()
    {
        await FederationServer.DisposeAsync();
        await Idp1.DisposeAsync();
        await Idp2.DisposeAsync();
        _idp1SamlSigningCert?.Dispose();
        _idp2SamlSigningCert?.Dispose();
        _spSamlSigningCert?.Dispose();
    }

    public static Task<SpacesFederationTestHost> CreateAsync(WebServerFixture webServerFixture, string name, Ct ct) =>
        CreateAsync(webServerFixture, name, _ => { }, options => options.KeyManagement.Enabled = false, ct);

    // Caching-wrapper variant used by the "_when_store_is_cached" runtime change/add/remove
    // scenarios. Mirrors SpacesDynamicProvidersTests.CreateCachingWrapperFixtureAsync
    // (AddIdentityProviderStoreCache + a short IdentityProviderCacheDuration); callers must
    // wait past cacheDuration after an admin change before observing it, following
    // update_in_space_a_does_not_affect_space_b_when_store_is_caching_wrapper.
    public static Task<SpacesFederationTestHost> CreateWithCachingWrapperAsync(
        WebServerFixture webServerFixture, string name, TimeSpan cacheDuration, Ct ct) =>
        CreateAsync(
            webServerFixture,
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
            },
            ct);

    private static async Task<SpacesFederationTestHost> CreateAsync(
        WebServerFixture webServerFixture,
        string name,
        Action<IIdentityServerBuilder> configureIdentityProviderStore,
        Action<IdentityServerOptions> configureOptions,
        Ct ct)
    {
        var dbName = $"msfed_{name}_{Guid.NewGuid():N}";
        var output = TestContext.Current.TestOutputHelper!;

        var mainHostAlias = $"ms{Guid.NewGuid():N}"[..10];
        var idp1HostAlias = $"idp1{Guid.NewGuid():N}"[..10];
        var idp2HostAlias = $"idp2{Guid.NewGuid():N}"[..10];

        // Host aliases for the (optional) host-resolved spaces, registered on the
        // federation server lazily by CreateHostResolvedSpacesAsync. Generated up front so
        // the IdP clients' redirect URIs can include them even though the corresponding
        // host-resolved space may never be created for a given test.
        var hostSpaceAHostAlias = $"h{Guid.NewGuid():N}"[..10] + ".dev.localhost";
        var hostSpaceBHostAlias = $"h{Guid.NewGuid():N}"[..10] + ".dev.localhost";

        // Shared clock for the SAML flows: Sustainsys (used on the IdP side) signs
        // assertions using the real system clock regardless of this TimeProvider, but the
        // SP side (federation server) validates assertion timestamps through the injected
        // TimeProvider. Freezing it at "now" keeps both sides consistent without drifting
        // during the test, mirroring SamlDynamicProviderFixture.
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);

        // Each IdP gets its own stable signing certificate (known before the IdP host is
        // even built) so the federation server's per-space SamlProvider can be seeded with
        // the matching public key up front.
        var idp1SigningCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider, "CN=Idp1Saml");
        var idp2SigningCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider, "CN=Idp2Saml");
        var idp1SamlCertBase64 = Convert.ToBase64String(idp1SigningCert.Export(X509ContentType.Cert));
        var idp2SamlCertBase64 = Convert.ToBase64String(idp2SigningCert.Export(X509ContentType.Cert));

        // The SP's own signing certificate (with private key), used only to sign OUTBOUND
        // requests the federation server sends (SP-initiated SLO LogoutRequests). Shared by
        // both spaces' dynamic SamlProviders; it identifies the SP, not either IdP.
        var spSigningCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider, "CN=FederationSp");
        var spSamlSigningCertBase64 = Convert.ToBase64String(spSigningCert.Export(X509ContentType.Pfx));

        var federationServer = new KestrelBasedTestServer(
            mainHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-federation"),
            services =>
            {
                services.AddRouting();
                services.AddSpaces();
                services.AddSingleton<TimeProvider>(fakeTimeProvider);

                var builder = services.AddIdentityServer(configureOptions)
                    .AddDeveloperSigningCredential(persistKey: false)
                    .AddSamlDynamicProvider()
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = SpaceBClientId,
                            AllowedGrantTypes = GrantTypes.Code,
                            RequireClientSecret = false,
                            RequirePkce = true,
                            RequireConsent = false,
                            RedirectUris = { "https://rp.example/callback" },
                            AllowedScopes = { "openid" }
                        }
                    ])
                    .AddInMemoryIdentityResources([new IdentityResources.OpenId()]);

                builder.AddStorage(storage => storage.AddSqliteInMemory(dbName))
                    .AddConfigurationStorage()
                    .AddOperationalStorage();

                configureIdentityProviderStore(builder);

                services.ConfigureAll<OpenIdConnectOptions>(options =>
                {
                    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                });
            },
            webapp =>
            {
                webapp.UseSpaceResolution();
                webapp.UseIdentityServer();

                // Terminal test-only endpoints mirroring the template ExternalLogin
                // Challenge/Callback pages and the Logout page, implemented as plain
                // middleware since endpoint routing runs before space path rewrites are
                // visible.
                webapp.Use(async (ctx, next) => await HandleTestEndpointsAsync(ctx, next));
            });

        var idp1 = new KestrelBasedTestServer(
            idp1HostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-idp1"),
            services => ConfigureIdp(services, federationServer, hostSpaceAHostAlias, hostSpaceBHostAlias, fakeTimeProvider, idp1SigningCert),
            webapp => ConfigureIdpPipeline(webapp, Idp1Subject));

        var idp2 = new KestrelBasedTestServer(
            idp2HostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-idp2"),
            services => ConfigureIdp(services, federationServer, hostSpaceAHostAlias, hostSpaceBHostAlias, fakeTimeProvider, idp2SigningCert),
            webapp => ConfigureIdpPipeline(webapp, Idp2Subject));

        await idp1.StartAsync();
        await idp2.StartAsync();
        await federationServer.StartAsync();

        var schema = federationServer.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        var spaceAdmin = federationServer.GetRequiredService<ISpaceAdmin>();

        var createSpaceAResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] },
            ct);
        createSpaceAResult.IsSuccess.ShouldBeTrue($"Create space A failed: {createSpaceAResult}");

        var createSpaceBResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] },
            ct);
        createSpaceBResult.IsSuccess.ShouldBeTrue($"Create space B failed: {createSpaceBResult}");

        var host = new SpacesFederationTestHost
        {
            FederationServer = federationServer,
            Idp1 = idp1,
            Idp2 = idp2,
            SpaceAdmin = spaceAdmin,
            SpaceAId = createSpaceAResult.Id!,
            SpaceBId = createSpaceBResult.Id!,
            HostSpaceAHostAlias = hostSpaceAHostAlias,
            HostSpaceBHostAlias = hostSpaceBHostAlias,
            Idp1SamlSigningCertificateBase64 = idp1SamlCertBase64,
            Idp2SamlSigningCertificateBase64 = idp2SamlCertBase64,
            SpSamlSigningCertificateBase64 = spSamlSigningCertBase64
        };
        host._idp1SamlSigningCert = idp1SigningCert;
        host._idp2SamlSigningCert = idp2SigningCert;
        host._spSamlSigningCert = spSigningCert;
        return host;
    }

    private static void ConfigureIdp(
        IServiceCollection services, KestrelBasedTestServer federationServer, string hostSpaceAHostAlias, string hostSpaceBHostAlias,
        FakeTimeProvider fakeTimeProvider, X509Certificate2 samlSigningCert)
    {
        services.AddRouting();
        services.AddAuthorization();
        services.AddSingleton<TimeProvider>(fakeTimeProvider);

        var hostSpaceAOrigin = new UriBuilder(federationServer.BaseAddress) { Host = hostSpaceAHostAlias }.Uri;
        var hostSpaceBOrigin = new UriBuilder(federationServer.BaseAddress) { Host = hostSpaceBHostAlias }.Uri;

        // SP entity IDs and ACS URLs for BOTH spaces are pre-registered on EVERY IdP (not
        // just the one a space currently points at), mirroring the OIDC client's redirect
        // URIs above. This lets the runtime-repoint scenarios (space 1 switched from IdP 1
        // to IdP 2) work without touching this fixture again.
        var spaceASpEntityId = federationServer.BuildUrl("/t/space-a").ToString().TrimEnd('/');
        var spaceBSpEntityId = federationServer.BuildUrl("/t/space-b").ToString().TrimEnd('/');
        var spaceAAcsUrl = federationServer.BuildUrl($"/t/space-a/federation/{SamlScheme}/Saml2/Acs").ToString();
        var spaceBAcsUrl = federationServer.BuildUrl($"/t/space-b/federation/{SamlScheme}/Saml2/Acs").ToString();

        services.AddIdentityServer(options =>
            {
                options.KeyManagement.Enabled = false;
                // Auto-login acts as the login page for BOTH the OIDC authorize endpoint
                // (unused here; the OIDC driver signs in at /login beforehand) and the SAML
                // SSO endpoint, which redirects here when the caller is unauthenticated.
                options.UserInteraction.LoginUrl = "/auto-login";
            })
            .AddSigningCredential(samlSigningCert)
            .AddSaml()
            .AddInMemorySamlServiceProviders([
                new SamlServiceProvider
                {
                    EntityId = spaceASpEntityId,
                    Enabled = true,
                    AllowedScopes = new HashSet<string> { "openid" },
                    AssertionConsumerServiceUrls =
                    [
                        new IndexedEndpoint { Location = spaceAAcsUrl, Binding = SamlBinding.HttpPost, Index = 0, IsDefault = true }
                    ],
                    SigningBehavior = SamlSigningBehavior.SignAssertion,
                    RequireSignedAuthnRequests = false
                },
                new SamlServiceProvider
                {
                    EntityId = spaceBSpEntityId,
                    Enabled = true,
                    AllowedScopes = new HashSet<string> { "openid" },
                    AssertionConsumerServiceUrls =
                    [
                        new IndexedEndpoint { Location = spaceBAcsUrl, Binding = SamlBinding.HttpPost, Index = 0, IsDefault = true }
                    ],
                    SigningBehavior = SamlSigningBehavior.SignAssertion,
                    RequireSignedAuthnRequests = false
                }
            ])
            .AddInMemoryClients([
                new Client
                {
                    ClientId = "client",
                    AllowedGrantTypes = GrantTypes.Code,
                    RequireClientSecret = false,
                    RequirePkce = true,
                    RequireConsent = false,
                    RedirectUris =
                    {
                        federationServer.BuildUrl($"/t/space-a/federation/{OidcScheme}/signin").ToString(),
                        federationServer.BuildUrl($"/t/space-b/federation/{OidcScheme}/signin").ToString(),
                        new Uri(hostSpaceAOrigin, $"/federation/{OidcScheme}/signin").ToString(),
                        new Uri(hostSpaceBOrigin, $"/federation/{OidcScheme}/signin").ToString()
                    },
                    AllowedScopes = { "openid" }
                }
            ])
            .AddInMemoryIdentityResources([new IdentityResources.OpenId()]);
    }

    private static void ConfigureIdpPipeline(WebAppWrapper webapp, string fixedSubject)
    {
        webapp.UseIdentityServer();

        // The query string is deliberately ignored: each remote IdP issues a FIXED
        // per-IdP identity regardless of anything the test driver sends, so a
        // misdirected challenge surfaces as the wrong subject rather than silently
        // succeeding.
        webapp.MapGet("/login", async ctx => await ctx.SignInAsync(new IdentityServerUser(fixedSubject).CreatePrincipal()));

        // Auto-login: immediately signs in the FIXED per-IdP subject and redirects to
        // returnUrl. Configured as UserInteraction.LoginUrl, so the SAML SSO endpoint
        // redirects here when unauthenticated, mirroring SamlDynamicProviderFixture.
        webapp.MapGet("/auto-login", async ctx =>
        {
            var returnUrl = ctx.Request.Query["ReturnUrl"].FirstOrDefault() ?? "/";
            await ctx.SignInAsync(new IdentityServerUser(fixedSubject).CreatePrincipal());
            ctx.Response.Redirect(returnUrl);
        });
    }

    private static async Task HandleTestEndpointsAsync(HttpContext ctx, RequestDelegate next)
    {
        var path = ctx.Request.Path.Value ?? "";

        if (path == "/test/challenge")
        {
            var scheme = ctx.Request.Query["scheme"].FirstOrDefault() ?? OidcScheme;
            await ctx.ChallengeAsync(scheme,
                new AuthenticationProperties
                {
                    RedirectUri = ctx.Request.PathBase + "/test/finish",
                    Items = { ["scheme"] = scheme }
                });
            return;
        }

        if (path == "/test/finish")
        {
            var external = await ctx.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            if (external.Succeeded)
            {
                var scheme = external.Properties?.Items["scheme"] ?? "unknown";
                // OIDC principals carry "sub"; SAML principals carry the NameID as
                // ClaimTypes.NameIdentifier (see AcsCommand). Either is the distinct
                // per-IdP identity the isolation tests assert on.
                var subjectClaim = external.Principal!.FindFirst("sub") ?? external.Principal!.FindFirst(ClaimTypes.NameIdentifier);
                var subject = subjectClaim?.Value ?? "unknown";
                var issuer = subjectClaim?.Issuer ?? "unknown";

                var localIdentity = new ClaimsIdentity(
                    external.Principal.Claims.Where(c => c.Type != JwtClaimTypes.IdentityProvider)
                        .Append(new Claim(JwtClaimTypes.IdentityProvider, scheme)),
                    IdentityServerConstants.DefaultCookieAuthenticationScheme);
                var localPrincipal = new ClaimsPrincipal(localIdentity);

                await ctx.SignInAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme, localPrincipal, external.Properties);
                await ctx.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync($"{subject}|{issuer}");
            }
            else
            {
                ctx.Response.StatusCode = 401;
            }
            return;
        }

        if (path == "/test/logout")
        {
            var main = await ctx.AuthenticateAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);
            if (main.Succeeded)
            {
                var idp = main.Principal!.FindFirst(JwtClaimTypes.IdentityProvider)?.Value;
                await ctx.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);
                if (!string.IsNullOrEmpty(idp))
                {
                    await ctx.SignOutAsync(idp,
                        new AuthenticationProperties { RedirectUri = ctx.Request.PathBase + "/test/finish" });
                    return;
                }
            }

            ctx.Response.StatusCode = 200;
            return;
        }

        await next(ctx);
    }

    // Registers the two host aliases (pre-generated in CreateAsync so the IdP clients'
    // redirect URIs already include them) on the federation server and creates one
    // host-resolved space per alias (SpaceMatchPattern.Origin), mirroring
    // SpacesDynamicProvidersTests.challenge_in_space_resolved_by_host_should_not_leak_other_spaces_provider.
    // Separate from the path-resolved space A / space B created in CreateAsync.
    public async Task<(Uri HostAOrigin, SpaceId HostASpaceId, Uri HostBOrigin, SpaceId HostBSpaceId)> CreateHostResolvedSpacesAsync(Ct ct)
    {
        FederationServer.RegisterHostAlias(HostSpaceAHostAlias);
        FederationServer.RegisterHostAlias(HostSpaceBHostAlias);

        var originA = new UriBuilder(FederationServer.BaseAddress) { Host = HostSpaceAHostAlias }.Uri;
        var originB = new UriBuilder(FederationServer.BaseAddress) { Host = HostSpaceBHostAlias }.Uri;

        var createHostSpaceAResult = await SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Host Space A", MatchPatterns = [new SpaceMatchPattern { Origin = originA.GetLeftPart(UriPartial.Authority) }] },
            ct);
        createHostSpaceAResult.IsSuccess.ShouldBeTrue($"Create host space A failed: {createHostSpaceAResult}");

        var createHostSpaceBResult = await SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Host Space B", MatchPatterns = [new SpaceMatchPattern { Origin = originB.GetLeftPart(UriPartial.Authority) }] },
            ct);
        createHostSpaceBResult.IsSuccess.ShouldBeTrue($"Create host space B failed: {createHostSpaceBResult}");

        return (originA, createHostSpaceAResult.Id!.Value, originB, createHostSpaceBResult.Id!.Value);
    }

    public async Task<IdentityProviderId> CreateOidcProviderForLoginAsync(SpaceId spaceId, string authority, string clientId, Ct ct)
    {
        using var scope = FederationServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var config = new CreateIdentityProvider { Scheme = OidcScheme, Type = "oidc" };
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.Authority)), authority);
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.ClientId)), clientId);
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.ResponseType)), "code");

            var result = await admin.CreateAsync(config, ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for space {spaceId}: {result}");
            return result.Id!;
        }
    }

    public async Task UpdateOidcProviderAuthorityAsync(SpaceId spaceId, IdentityProviderId providerId, string newAuthority, Ct ct)
    {
        using var scope = FederationServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var getResult = await admin.GetAsync(providerId, ct);
            getResult.Found.ShouldBeTrue($"Provider {providerId} not found in space {spaceId}");

            var toUpdate = getResult.Item.ToUpdate();
            toUpdate.ExtendedProperties.Set(AttributeCode.Create(nameof(OidcProvider.Authority)), newAuthority);

            var updateResult = await admin.UpdateAsync(providerId, toUpdate, getResult.Version!, ct);
            updateResult.IsSuccess.ShouldBeTrue($"Update failed for space {spaceId}: {updateResult}");
        }
    }

    public async Task DeleteOidcProviderAsync(SpaceId spaceId, IdentityProviderId providerId, Ct ct)
    {
        using var scope = FederationServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var deleteResult = await admin.DeleteAsync(providerId, ct);
            deleteResult.IsSuccess.ShouldBeTrue($"Delete failed for space {spaceId}: {deleteResult}");
        }
    }

    // IIdentityProviderAdmin.DeleteAsync is protocol-agnostic (identified by IdentityProviderId
    // only), so this is a straight alias of DeleteOidcProviderAsync, named for readability at
    // SAML call sites.
    public Task DeleteSamlProviderAsync(SpaceId spaceId, IdentityProviderId providerId, Ct ct) =>
        DeleteOidcProviderAsync(spaceId, providerId, ct);

    // Repoints an existing dynamic SamlProvider at a different IdP (IdpEntityId,
    // SingleSignOnServiceUrl, SingleLogoutServiceUrl, and the trusted signing certificate),
    // mirroring UpdateOidcProviderAuthorityAsync.
    public async Task UpdateSamlProviderIdpAsync(
        SpaceId spaceId, IdentityProviderId providerId, KestrelBasedTestServer newIdpServer, string newIdpSamlCertBase64, Ct ct)
    {
        using var scope = FederationServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        var newIdpUri = newIdpServer.BuildUrl("/").ToString().TrimEnd('/');

        using (accessor.SetSpace(spaceId))
        {
            var getResult = await admin.GetAsync(providerId, ct);
            getResult.Found.ShouldBeTrue($"Provider {providerId} not found in space {spaceId}");

            var toUpdate = getResult.Item.ToUpdate();
            toUpdate.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.IdpEntityId)), $"{newIdpUri}/Saml2");
            toUpdate.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SingleSignOnServiceUrl)), $"{newIdpUri}/Saml2/SSO");
            toUpdate.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SingleLogoutServiceUrl)), $"{newIdpUri}/Saml2/SLO");
            toUpdate.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SigningCertificateBase64)), newIdpSamlCertBase64);

            var updateResult = await admin.UpdateAsync(providerId, toUpdate, getResult.Version!, ct);
            updateResult.IsSuccess.ShouldBeTrue($"Update failed for space {spaceId}: {updateResult}");
        }
    }

    // Creates a dynamic SamlProvider in the given space pointing at idpServer, trusting
    // idpSamlCertBase64 for assertion signature validation. urlPrefix ("/t/space-a" or
    // "/t/space-b") must match one of the two SP entity IDs / ACS URLs pre-registered as
    // SamlServiceProviders on EVERY IdP in ConfigureIdp.
    public async Task<IdentityProviderId> CreateSamlProviderForLoginAsync(
        SpaceId spaceId, string urlPrefix, KestrelBasedTestServer idpServer, string idpSamlCertBase64, Ct ct)
    {
        using var scope = FederationServer.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        var idpUri = idpServer.BuildUrl("/").ToString().TrimEnd('/');
        var spEntityId = FederationServer.BuildUrl(urlPrefix).ToString().TrimEnd('/');

        using (accessor.SetSpace(spaceId))
        {
            var config = new CreateIdentityProvider { Scheme = SamlScheme, Type = "saml" };
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.IdpEntityId)), $"{idpUri}/Saml2");
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SingleSignOnServiceUrl)), $"{idpUri}/Saml2/SSO");
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SingleLogoutServiceUrl)), $"{idpUri}/Saml2/SLO");
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SigningCertificateBase64)), idpSamlCertBase64);
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.BindingType)), "redirect");
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.WantAssertionsSigned)), "false");
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SpEntityId)), spEntityId);
            // SP signing certificate: required for Saml2Handler.SignOutAsync to build an
            // outbound SP-initiated LogoutRequest (LogOutCommand.InitiateLogout requires
            // options.SPOptions.SigningServiceCertificate != null); not needed for sign-in alone.
            config.ExtendedProperties.Set(AttributeCode.Create(nameof(SamlProvider.SpSigningCertificateBase64)), SpSamlSigningCertificateBase64);

            var result = await admin.CreateAsync(config, ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for space {spaceId}: {result}");
            return result.Id!;
        }
    }

    public static HttpClient CreateBrowserClient(KestrelBasedTestServer server)
    {
#pragma warning disable CA2000 // Ownership transferred to HttpClient via disposeHandler: true
        var browserHandler = new BrowserHandler(server.CreateHandler(allowAutoRedirect: false))
        {
            AllowAutoRedirect = false
        };
#pragma warning restore CA2000
        return new HttpClient(browserHandler, disposeHandler: true) { BaseAddress = server.BaseAddress };
    }

    // For a host-resolved space, the space is identified by the request's Host header
    // (the alias origin), not by a /t/{space} path prefix, so the client's BaseAddress
    // is the alias origin rather than the server's own BaseAddress. The shared in-process
    // handler still routes to the same physical server: TestIsolationService.CreateHandler
    // routes by Host header on the shared port.
    public static HttpClient CreateBrowserClientForOrigin(KestrelBasedTestServer server, Uri origin)
    {
#pragma warning disable CA2000 // Ownership transferred to HttpClient via disposeHandler: true
        var browserHandler = new BrowserHandler(server.CreateHandler(allowAutoRedirect: false))
        {
            AllowAutoRedirect = false
        };
#pragma warning restore CA2000
        return new HttpClient(browserHandler, disposeHandler: true) { BaseAddress = origin };
    }

    public static async Task<OidcSignInResult> RunOidcSignInAsync(
        KestrelBasedTestServer federationServer, KestrelBasedTestServer idpServer, string urlPrefix)
    {
        using var mainClient = CreateBrowserClient(federationServer);
        return await RunOidcSignInAsync(mainClient, idpServer, urlPrefix);
    }

    public static async Task<OidcSignInResult> RunOidcSignInAsync(
        HttpClient mainClient, KestrelBasedTestServer idpServer, string urlPrefix)
    {
        using var idpClient = CreateBrowserClient(idpServer);

        var challenge = await mainClient.GetAsync($"{urlPrefix}/test/challenge");
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var authorizeUrl = challenge.Headers.Location!.ToString();

        (await idpClient.GetAsync("/login")).EnsureSuccessStatusCode();
        var idpAuthorize = await idpClient.GetAsync(authorizeUrl);
        idpAuthorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        var redirectUri = idpAuthorize.Headers.Location!.ToString();

        var callback = await mainClient.GetAsync(redirectUri);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finishUri = callback.Headers.Location!.ToString();

        var finish = await mainClient.GetAsync(finishUri);
        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await finish.Content.ReadAsStringAsync();
        var parts = body.Split('|');

        return new OidcSignInResult
        {
            ChallengeRedirectHost = new Uri(authorizeUrl).Host,
            Subject = parts[0],
            Issuer = parts.Length > 1 ? parts[1] : "unknown"
        };
    }

    // Drives a full sign-in then, on the SAME browser client/cookie container (so the
    // main cookie set at /test/finish is present), hits /test/logout and returns the
    // resulting end-session redirect Uri so the caller can assert the destination host.
    public static async Task<Uri> RunOidcSignInThenLogoutAsync(
        KestrelBasedTestServer federationServer, KestrelBasedTestServer idpServer, string urlPrefix)
    {
        using var mainClient = CreateBrowserClient(federationServer);
        await RunOidcSignInAsync(mainClient, idpServer, urlPrefix);

        var logout = await mainClient.GetAsync($"{urlPrefix}/test/logout");
        logout.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return logout.Headers.Location!;
    }

    // Step-decomposed sign-in, used by the interleaved concurrency scenario to drive
    // several independent flows' challenge / login / callback / finish steps concurrently
    // (Task.WhenAll per step) without duplicating RunOidcSignInAsync's all-in-one shape.
    public static async Task<string> ChallengeAsync(HttpClient mainClient, string urlPrefix)
    {
        var challenge = await mainClient.GetAsync($"{urlPrefix}/test/challenge");
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return challenge.Headers.Location!.ToString();
    }

    public static async Task<string> LoginAtIdpAsync(HttpClient idpClient, string authorizeUrl)
    {
        (await idpClient.GetAsync("/login")).EnsureSuccessStatusCode();
        var idpAuthorize = await idpClient.GetAsync(authorizeUrl);
        idpAuthorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        return idpAuthorize.Headers.Location!.ToString();
    }

    public static async Task<string> CallbackAsync(HttpClient mainClient, string redirectUri)
    {
        var callback = await mainClient.GetAsync(redirectUri);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return callback.Headers.Location!.ToString();
    }

    public static async Task<OidcSignInResult> FinishAsync(HttpClient mainClient, string finishUri, string authorizeUrl)
    {
        var finish = await mainClient.GetAsync(finishUri);
        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await finish.Content.ReadAsStringAsync();
        var parts = body.Split('|');

        return new OidcSignInResult
        {
            ChallengeRedirectHost = new Uri(authorizeUrl).Host,
            Subject = parts[0],
            Issuer = parts.Length > 1 ? parts[1] : "unknown"
        };
    }

    // Drives a full SAML sign-in through the SP-initiated redirect-binding challenge,
    // the IdP's SSO endpoint, its /auto-login (issuing a FIXED per-IdP NameID), the
    // POST-binding response back to the federation server's ACS, and /test/finish.
    // Ported from SamlDynamicProviderFixture's FollowRedirectChainAsync/SubmitHtmlFormAsync,
    // decomposed into explicit steps (rather than a single auto-redirecting client) so the
    // challenge redirect host can be asserted before the flow continues, mirroring
    // RunOidcSignInAsync. mainClient and idpClient are deliberately separate HttpClients
    // with independent cookie containers (one per physical host), same as the OIDC driver;
    // this is also the first proof point for the shared-port host-alias risk: both the
    // SSO redirect (mainClient -> idp host alias) and the ACS POST (idp host's HTML form
    // submitted via mainClient -> federation host alias) must route correctly under
    // KestrelBasedTestServer's single shared port.
    public static async Task<OidcSignInResult> RunSamlSignInAsync(
        KestrelBasedTestServer federationServer, KestrelBasedTestServer idpServer, string urlPrefix)
    {
        using var mainClient = CreateBrowserClient(federationServer);
        return await RunSamlSignInAsync(mainClient, idpServer, urlPrefix);
    }

    public static async Task<OidcSignInResult> RunSamlSignInAsync(
        HttpClient mainClient, KestrelBasedTestServer idpServer, string urlPrefix)
    {
        using var idpClient = CreateBrowserClient(idpServer);

        // Step 1: challenge the dynamic SAML provider. Saml2Handler issues an HTTP-Redirect
        // binding AuthnRequest to the IdP's SingleSignOnServiceUrl.
        var challenge = await mainClient.GetAsync($"{urlPrefix}/test/challenge?scheme={SamlScheme}");
        challenge.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        var ssoUrl = challenge.Headers.Location!.ToString();

        // Step 2: the IdP is unauthenticated, so its SSO endpoint redirects to /auto-login.
        var ssoRequest = await idpClient.GetAsync(ssoUrl);
        ssoRequest.StatusCode.ShouldBe(HttpStatusCode.SeeOther,
            "unauthenticated SSO request must redirect to the IdP's login page");
        var autoLoginUrl = ssoRequest.Headers.Location!.ToString();

        // Step 3: /auto-login signs in the FIXED per-IdP subject and redirects back to the
        // original SSO request (the query string is deliberately ignored by /auto-login,
        // same principle as the OIDC /login).
        var autoLoginResponse = await idpClient.GetAsync(autoLoginUrl);
        autoLoginResponse.StatusCode.ShouldBe(HttpStatusCode.Found);
        var ssoUrlAgain = autoLoginResponse.Headers.Location!.ToString();

        // Step 4: now authenticated, the SSO endpoint returns the HTTP-POST binding
        // auto-submit form (SAMLResponse + RelayState) targeting the federation server's
        // ACS URL.
        var ssoResponse = await idpClient.GetAsync(ssoUrlAgain);
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (acsUrl, formData) = ExtractSamlPostBindingForm(await ssoResponse.Content.ReadAsStringAsync());

        // Step 5: POST the SAML response to the ACS URL via mainClient (not idpClient), so
        // the federation server's own signin-state cookie (set during step 1) is presented.
        // This is the second half of the shared-port routing proof: the form's absolute
        // action URL targets the federation host alias even though mainClient most recently
        // talked to the IdP host alias in step 1.
        using var formContent = new FormUrlEncodedContent(formData);
        var acsResponse = await mainClient.PostAsync(acsUrl, formContent);
        acsResponse.StatusCode.ShouldBe(HttpStatusCode.SeeOther,
            "a successful ACS POST must complete the challenge and redirect to /test/finish");
        var finishUri = acsResponse.Headers.Location!.ToString();

        var finish = await mainClient.GetAsync(finishUri);
        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await finish.Content.ReadAsStringAsync();
        var parts = body.Split('|');

        return new OidcSignInResult
        {
            ChallengeRedirectHost = new Uri(ssoUrl).Host,
            Subject = parts[0],
            Issuer = parts.Length > 1 ? parts[1] : "unknown"
        };
    }

    // Drives a full SAML sign-in then, on the SAME browser client/cookie container (so the
    // main cookie set at /test/finish is present), hits /test/logout and returns the
    // resulting SP-initiated LogoutRequest redirect Uri so the caller can assert the
    // destination host. Mirrors RunOidcSignInThenLogoutAsync.
    public static async Task<Uri> RunSamlSignInThenLogoutAsync(
        KestrelBasedTestServer federationServer, KestrelBasedTestServer idpServer, string urlPrefix)
    {
        using var mainClient = CreateBrowserClient(federationServer);
        await RunSamlSignInAsync(mainClient, idpServer, urlPrefix);

        var logout = await mainClient.GetAsync($"{urlPrefix}/test/logout");
        logout.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        return logout.Headers.Location!;
    }

    // Step-decomposed SAML sign-in, mirroring the OIDC ChallengeAsync / LoginAtIdpAsync /
    // CallbackAsync / FinishAsync split, used by the interleaved SAML concurrency scenario
    // to drive several independent flows' steps concurrently (Task.WhenAll per step)
    // without duplicating RunSamlSignInAsync's all-in-one shape.
    public static async Task<string> SamlChallengeAsync(HttpClient mainClient, string urlPrefix)
    {
        var challenge = await mainClient.GetAsync($"{urlPrefix}/test/challenge?scheme={SamlScheme}");
        challenge.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        return challenge.Headers.Location!.ToString();
    }

    public static async Task<string> SamlSsoRequestAsync(HttpClient idpClient, string ssoUrl)
    {
        var ssoRequest = await idpClient.GetAsync(ssoUrl);
        ssoRequest.StatusCode.ShouldBe(HttpStatusCode.SeeOther,
            "unauthenticated SSO request must redirect to the IdP's login page");
        return ssoRequest.Headers.Location!.ToString();
    }

    public static async Task<string> SamlAutoLoginAsync(HttpClient idpClient, string autoLoginUrl)
    {
        var autoLoginResponse = await idpClient.GetAsync(autoLoginUrl);
        autoLoginResponse.StatusCode.ShouldBe(HttpStatusCode.Found);
        return autoLoginResponse.Headers.Location!.ToString();
    }

    public static async Task<(string AcsUrl, Dictionary<string, string> FormData)> SamlSsoResponseAsync(HttpClient idpClient, string ssoUrlAgain)
    {
        var ssoResponse = await idpClient.GetAsync(ssoUrlAgain);
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        return ExtractSamlPostBindingForm(await ssoResponse.Content.ReadAsStringAsync());
    }

    public static async Task<string> SamlAcsAsync(HttpClient mainClient, string acsUrl, Dictionary<string, string> formData)
    {
        using var formContent = new FormUrlEncodedContent(formData);
        var acsResponse = await mainClient.PostAsync(acsUrl, formContent);
        acsResponse.StatusCode.ShouldBe(HttpStatusCode.SeeOther,
            "a successful ACS POST must complete the challenge and redirect to /test/finish");
        return acsResponse.Headers.Location!.ToString();
    }

    // Extracts the hidden form fields and form action (ACS URL) from a SAML HTTP-POST
    private static (string ActionUrl, Dictionary<string, string> FormData) ExtractSamlPostBindingForm(string html)
    {
        var actionMatch = Regex.Match(html, @"<form[^>]+action=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        actionMatch.Success.ShouldBeTrue("Form action not found in SAML POST-binding HTML");
        var actionUrl = HttpUtility.HtmlDecode(actionMatch.Groups[1].Value);

        var formData = new Dictionary<string, string>();
        var inputTagMatches = Regex.Matches(html, @"<input\b[^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match inputTag in inputTagMatches)
        {
            var tag = inputTag.Value;
            var typeMatch = Regex.Match(tag, @"\btype=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            if (!typeMatch.Success || !typeMatch.Groups[1].Value.Equals("hidden", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nameMatch = Regex.Match(tag, @"\bname=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            if (!nameMatch.Success)
            {
                continue;
            }

            var valueMatch = Regex.Match(tag, @"\bvalue=['""]([^'""]*)['""]", RegexOptions.IgnoreCase);
            var value = valueMatch.Success ? HttpUtility.HtmlDecode(valueMatch.Groups[1].Value) : string.Empty;
            formData[nameMatch.Groups[1].Value] = value;
        }

        return (actionUrl, formData);
    }
}

internal sealed class OidcSignInResult
{
    public required string ChallengeRedirectHost { get; init; }
    public required string Subject { get; init; }
    public required string Issuer { get; init; }
}

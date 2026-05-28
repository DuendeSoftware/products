// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

/// <summary>
/// Three-host Kestrel fixture for end-to-end dynamic SAML provider tests.
///
/// Webapp 1 (client): OIDC client app → Webapp 2
/// Webapp 2 (IS SP+IdP): IdentityServer with dynamic SAML provider (EF store) → Webapp 3
/// Webapp 3 (IS IdP): IdentityServer SAML IdP with auto-login endpoint (EF store)
///
/// Startup order: Webapp 3 → Webapp 2 → Webapp 1 (each needs the previous URI).
/// Both IdentityServer hosts use EF with SQLite in-memory databases. Cross-references
/// (SAML service providers, OIDC clients) are seeded into the EF stores after each host starts.
/// </summary>
internal sealed class SamlDynamicProviderFixture(ITestOutputHelper output, Action<IServiceCollection>? additionalSpServices = null) : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    // SQLite in-memory connections — kept open for the lifetime of the fixture so the DBs persist.
    // EF Core's UseInMemoryDatabase doesn't support ExecuteDeleteAsync (used by PAR store).
    // Each host gets its own connection so their schemas don't collide.
    private readonly SqliteConnection _idpSqliteConnection = new("Data Source=:memory:");
    private readonly SqliteConnection _spSqliteConnection = new("Data Source=:memory:");

    // The three Kestrel hosts
    public KestrelTestHost? IdpHost;    // Webapp 3 — SAML IdP
    public KestrelTestHost? SpHost;     // Webapp 2 — IdentityServer (SAML SP + OIDC IdP)
    public KestrelTestHost? ClientHost; // Webapp 1 — OIDC client app

    // Shared browser client with a single CookieContainer so cookies flow across all 3 hosts
    public HttpClient? BrowserClient;

    // The test user signed in at Webapp 3
    public const string TestUserSub = "test-user";
    public const string TestUserName = "Test User";
    public const string TestUserEmail = "test@example.com";

    public async ValueTask InitializeAsync()
    {
        // Open the shared SQLite in-memory connections — they stay open so the DBs persist
        // across multiple DbContext instances for the lifetime of the fixture.
        await _idpSqliteConnection.OpenAsync(_ct);
        await _spSqliteConnection.OpenAsync(_ct);

        // Use current time — Sustainsys does not use TimeProvider abstraction
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);

        // Load the stable signing cert (PFX with private key) for Webapp 3 (IdP)
        var idpSigningCert = X509CertificateLoader.LoadPkcs12(
            Convert.FromBase64String(SamlFixture.StableSigningCert), null);

        // Export the public key only — Webapp 2 (SP) uses this to validate IdP signatures
        var idpPublicCertBytes = idpSigningCert.Export(X509ContentType.Cert);
        var idpPublicCertBase64 = Convert.ToBase64String(idpPublicCertBytes);

        // Step 1: Start Webapp 3 (SAML IdP) — _idpServiceProviders is empty at this point;
        // Webapp 2's ACS URL is added after Webapp 2 starts.
        await StartIdpHost(fakeTimeProvider, idpSigningCert);

        var idpUri = IdpHost!.Uri();

        // Step 2: Start Webapp 2 (IdentityServer SAML SP + OIDC IdP) backed by EF in-memory DB.
        // The SAML provider pointing to Webapp 3 is seeded into the ConfigurationDbContext
        // during startup, proving the EF IdentityProviderStore correctly maps SAML providers.
        await StartSpHost(fakeTimeProvider, idpUri, idpPublicCertBase64);

        var spUri = SpHost!.Uri();

        // Backfill: seed Webapp 2 as a SAML service provider into Webapp 3's EF configuration store
        await SeedServiceProviderIntoIdpAsync(spUri);

        // Step 3: Start Webapp 1 (OIDC client app) — spUri is now known
        await StartClientHost(spUri);

        var clientUri = ClientHost!.Uri();

        // Backfill: seed Webapp 1's OIDC client into Webapp 2's EF configuration store.
        await SeedClientIntoSpAsync(clientUri);

        // Create a shared browser client with a single CookieContainer so cookies are
        // shared across all 3 hosts during the redirect chain.
        var cookieContainer = new CookieContainer();
        var handler = new CookieHandler(
            new HttpClientHandler { AllowAutoRedirect = true },
            cookieContainer);
        BrowserClient = new HttpClient(handler);
    }

    private async Task StartIdpHost(FakeTimeProvider fakeTimeProvider, X509Certificate2 idpSigningCert)
    {
        IdpHost = await KestrelTestHost.Create(output,
            services =>
            {
                services.AddSingleton<TimeProvider>(fakeTimeProvider);
                services.AddSingleton<IDistributedCache>(
                    sp => new FakeDistributedCache(sp.GetRequiredService<TimeProvider>()));

                services.AddIdentityServer(options =>
                    {
                        // Auto-login endpoint acts as the login page
                        options.UserInteraction.LoginUrl = "/auto-login";
                        options.UserInteraction.LogoutUrl = "/account/logout";
                        options.UserInteraction.ConsentUrl = "/consent";
                        options.KeyManagement.Enabled = false;
                    })
                    .AddSigningCredential(idpSigningCert)
                    .AddConfigurationStore(options =>
                    {
                        options.ResolveDbContextOptions = (_, dbOptions) =>
                            dbOptions.UseSqlite(_idpSqliteConnection);
                    })
                    .AddOperationalStore(options =>
                    {
                        options.ResolveDbContextOptions = (_, dbOptions) =>
                            dbOptions.UseSqlite(_idpSqliteConnection);
                    })
                    .AddSaml();
            },
            app =>
            {
                app.UseIdentityServer();

                // Auto-login: immediately signs in the test user and redirects to returnUrl.
                // This is configured as the LoginUrl so the SAML sign-in endpoint redirects here
                // when the user is unauthenticated.
                app.MapGet("/auto-login", async (HttpContext ctx) =>
                {
                    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/";

                    var testUser = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(JwtClaimTypes.Subject, TestUserSub),
                        new Claim(JwtClaimTypes.Name, TestUserName),
                        new Claim(JwtClaimTypes.Email, TestUserEmail),
                        new Claim(JwtClaimTypes.AuthenticationMethod, "pwd")
                    ], "pwd", JwtClaimTypes.Name, JwtClaimTypes.Role));

                    await ctx.SignInAsync(testUser, new AuthenticationProperties());
                    ctx.Response.Redirect(returnUrl);
                });

                app.MapGet("/account/logout", () => Results.Ok());
                app.MapGet("/consent", () => Results.Ok());
            },
            _ct);

        // Create the EF tables and seed identity resources for the IdP
        await using var scope = IdpHost.ConfiguredServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await db.Database.EnsureCreatedAsync(_ct);

        var opDb = scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();
        var creator = opDb.GetService<IRelationalDatabaseCreator>()!;
        await creator.CreateTablesAsync(_ct);

        db.IdentityResources.Add(new IdentityResources.OpenId().ToEntity());
        db.IdentityResources.Add(new IdentityResources.Profile().ToEntity());

        await db.SaveChangesAsync(_ct);
    }

    private async Task StartSpHost(
        FakeTimeProvider fakeTimeProvider,
        string idpUri,
        string idpPublicCertBase64)
    {
        SpHost = await KestrelTestHost.Create(output,
            services =>
            {
                services.AddSingleton<TimeProvider>(fakeTimeProvider);
                services.AddSingleton<IDistributedCache>(
                    sp => new FakeDistributedCache(sp.GetRequiredService<TimeProvider>()));

                services.AddIdentityServer(options =>
                    {
                        options.UserInteraction.LoginUrl = "/account/login";
                        options.UserInteraction.LogoutUrl = "/account/logout";
                        options.UserInteraction.ConsentUrl = "/consent";
                        options.KeyManagement.Enabled = false;
                    })
                    .AddDeveloperSigningCredential(persistKey: false)
                    .AddConfigurationStore(options =>
                    {
                        options.ResolveDbContextOptions = (_, dbOptions) =>
                            dbOptions.UseSqlite(_spSqliteConnection);
                    })
                    .AddOperationalStore(options =>
                    {
                        options.ResolveDbContextOptions = (_, dbOptions) =>
                            dbOptions.UseSqlite(_spSqliteConnection);
                    })
                    .AddSamlDynamicProvider();

                additionalSpServices?.Invoke(services);
            },
            app =>
            {
                app.UseIdentityServer();

                // Login page: challenge the dynamic SAML provider, which redirects to Webapp 3.
                // The ReturnUrl from IdentityServer is passed through as the OIDC redirect_uri
                // so the flow completes back to the authorize callback after SAML authentication.
                app.MapGet("/account/login", async (HttpContext ctx) =>
                {
                    var returnUrl = ctx.Request.Query["ReturnUrl"].FirstOrDefault() ?? "/";
                    var props = new AuthenticationProperties { RedirectUri = returnUrl };
                    await ctx.ChallengeAsync("saml-idp", props);
                });

                app.MapGet("/account/logout", () => Results.Ok());
                app.MapGet("/consent", () => Results.Ok());
            },
            _ct);

        // Seed the SAML provider and identity resources into the EF configuration store
        await using var scope = SpHost.ConfiguredServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await db.Database.EnsureCreatedAsync(_ct);

        // Both DbContexts share the same SQLite connection. EnsureCreated on the second
        // context is a no-op because the database already "exists". Use CreateTables()
        // to add the operational store tables to the existing database.
        var opDb = scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();
        var creator = opDb.GetService<IRelationalDatabaseCreator>()!;
        await creator.CreateTablesAsync(_ct);

        db.IdentityProviders.Add(new SamlProvider
        {
            Scheme = "saml-idp",
            DisplayName = "SAML IdP (Webapp 3)",
            Enabled = true,
            IdpEntityId = $"{idpUri}/Saml2",
            SingleSignOnServiceUrl = $"{idpUri}/Saml2/SSO",
            SigningCertificateBase64 = idpPublicCertBase64,
            BindingType = "redirect",
            WantAssertionsSigned = false
        }.ToEntity());

        db.IdentityResources.Add(new IdentityResources.OpenId().ToEntity());
        db.IdentityResources.Add(new IdentityResources.Profile().ToEntity());

        await db.SaveChangesAsync(_ct);
    }

    private async Task SeedServiceProviderIntoIdpAsync(string spUri)
    {
        await using var scope = IdpHost!.ConfiguredServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        db.SamlServiceProviders.Add(new SamlServiceProvider
        {
            EntityId = spUri,
            DisplayName = "IdentityServer SP (Webapp 2)",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid", "profile" },
            AssertionConsumerServiceUrls =
            [
                new IndexedEndpoint
                {
                    Location = $"{spUri}/federation/saml-idp/Saml2/Acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            RequireSignedAuthnRequests = false
        }.ToEntity());

        await db.SaveChangesAsync(_ct);
    }

    private async Task SeedClientIntoSpAsync(string clientUri)
    {
        await using var scope = SpHost!.ConfiguredServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        db.Clients.Add(new Client
        {
            ClientId = "webapp1",
            ClientSecrets = [new Secret("secret".Sha256())],
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris = [$"{clientUri}/signin-oidc"],
            PostLogoutRedirectUris = [$"{clientUri}/signout-callback-oidc"],
            AllowedScopes = ["openid", "profile"],
            RequireConsent = false
        }.ToEntity());

        await db.SaveChangesAsync(_ct);
    }

    private async Task StartClientHost(string spUri) =>
        ClientHost = await KestrelTestHost.Create(output,
            services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    })
                    .AddCookie()
                    .AddOpenIdConnect(options =>
                    {
                        options.Authority = spUri;
                        options.ClientId = "webapp1";
                        options.ClientSecret = "secret";
                        options.ResponseType = "code";
                        options.Scope.Clear();
                        options.Scope.Add("openid");
                        options.Scope.Add("profile");
                        options.SaveTokens = true;
                        options.GetClaimsFromUserInfoEndpoint = true;
                        options.MapInboundClaims = false;
                        // Use a real HttpClientHandler since we're using Kestrel (not TestServer)
                        options.BackchannelHttpHandler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };
                        options.RequireHttpsMetadata = false;
                    });

                services.AddAuthorization();
                services.AddRouting();
            },
            app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();

                // Protected endpoint — triggers OIDC challenge when unauthenticated
                app.MapGet("/protected", async (HttpContext ctx) =>
                {
                    var sub = ctx.User.FindFirst(JwtClaimTypes.Subject)?.Value
                        ?? ctx.User.FindFirst("sub")?.Value;
                    var name = ctx.User.FindFirst(JwtClaimTypes.Name)?.Value
                        ?? ctx.User.FindFirst("name")?.Value;
                    await ctx.Response.WriteAsJsonAsync(new { sub, name });
                }).RequireAuthorization();
            },
            _ct);

    /// <summary>
    /// Follows the full redirect chain starting from <paramref name="startUrl"/>.
    /// Auto-redirects handle HTTP 302s. When an HTML auto-submit form is encountered
    /// (SAML POST binding or OIDC form_post), the form fields are extracted and POSTed manually.
    /// Returns the final response.
    /// </summary>
    public async Task<HttpResponseMessage> FollowRedirectChainAsync(string startUrl)
    {
        var response = await BrowserClient!.GetAsync(startUrl, _ct);

        // Handle up to 5 HTML form submissions (SAML POST + OIDC form_post)
        for (var i = 0; i < 5; i++)
        {
            if (!IsHtmlFormPost(response))
            {
                break;
            }

            response = await SubmitHtmlFormAsync(response);
        }

        return response;
    }

    private static bool IsHtmlFormPost(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return false;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        return contentType == "text/html";
    }

    private async Task<HttpResponseMessage> SubmitHtmlFormAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync(_ct);

        // Extract form action URL — handle both single and double quotes
        var actionMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<form[^>]+action=['""]([^'""]+)['""]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!actionMatch.Success)
        {
            return response;
        }

        var actionUrl = System.Web.HttpUtility.HtmlDecode(actionMatch.Groups[1].Value);

        // Extract all hidden input fields — attributes may appear in any order.
        // Strategy: find each <input> tag, then extract name and value attributes independently.
        var formData = new Dictionary<string, string>();
        var inputTagMatches = System.Text.RegularExpressions.Regex.Matches(
            html,
            @"<input\b[^>]*/?>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match inputTag in inputTagMatches)
        {
            var tag = inputTag.Value;

            // Only process hidden inputs
            var typeMatch = System.Text.RegularExpressions.Regex.Match(
                tag, @"\btype=['""]([^'""]+)['""]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!typeMatch.Success || !typeMatch.Groups[1].Value.Equals("hidden", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nameMatch = System.Text.RegularExpressions.Regex.Match(
                tag, @"\bname=['""]([^'""]+)['""]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var valueMatch = System.Text.RegularExpressions.Regex.Match(
                tag, @"\bvalue=['""]([^'""]*)['""]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!nameMatch.Success)
            {
                continue;
            }

            var name = nameMatch.Groups[1].Value;
            var value = valueMatch.Success
                ? System.Web.HttpUtility.HtmlDecode(valueMatch.Groups[1].Value)
                : string.Empty;

            // SAMLResponse is base64-encoded XML — re-encode from decoded XML
            if (name.Equals("SAMLResponse", StringComparison.OrdinalIgnoreCase))
            {
                var decodedBytes = Convert.FromBase64String(value);
                var xml = System.Text.Encoding.UTF8.GetString(decodedBytes);
                value = SamlTestHelpers.ConvertToBase64Encoded(xml);
            }
            else if (name.Equals("RelayState", StringComparison.OrdinalIgnoreCase))
            {
                value = System.Web.HttpUtility.UrlEncode(value);
            }

            formData[name] = value;
        }

        if (formData.Count == 0)
        {
            return response;
        }

        using var formContent = new FormUrlEncodedContent(formData);
        return await BrowserClient!.PostAsync(actionUrl, formContent, _ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (ClientHost != null)
        {
            await ClientHost.DisposeAsync();
        }

        if (SpHost != null)
        {
            await SpHost.DisposeAsync();
        }

        if (IdpHost != null)
        {
            await IdpHost.DisposeAsync();
        }

        BrowserClient?.Dispose();
        await _spSqliteConnection.DisposeAsync();
        await _idpSqliteConnection.DisposeAsync();
    }
}

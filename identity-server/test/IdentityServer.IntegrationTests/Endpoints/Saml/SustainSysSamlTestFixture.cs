// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Sustainsys.Saml2.AspNetCore2;
using IdentityProvider = Sustainsys.Saml2.IdentityProvider;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

internal class SustainSysSamlTestFixture(ITestOutputHelper output) : IAsyncLifetime
{
    public KestrelTestHost? IdpHost;
    public KestrelTestHost? SpHost;
    public HttpClient? BrowserClient;
    public X509Certificate2? SigningCertificate { get; private set; }

    private readonly List<SamlServiceProvider> _serviceProviders = [];
    private ClaimsPrincipal? _userToSignIn;
    private bool _shouldGenerateSigningCertificate;
    private bool _shouldRequireEncryptedAssertions;

    public async Task LoginUserAtIdentityProvider()
    {
        _userToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id"), new Claim("name", "Test User"), new Claim(JwtClaimTypes.AuthenticationMethod, "urn:oasis:names:tc:SAML:2.0:ac:classes:Password")], "Test"));
        await BrowserClient!.GetAsync($"{IdpHost!.Uri()}/__signin", CancellationToken.None);
    }

    public void GenerateSigningCertificate() =>
        // We cannot create this now because we need to defer creating it until after we've set
        // the fake time provider because the SustainSys library does not rely on TimeProvider
        // and we need to use current times
        _shouldGenerateSigningCertificate = true;

    public void RequireEncryptedAssertions() => _shouldRequireEncryptedAssertions = true;

    public async ValueTask InitializeAsync()
    {
        // need to use current time because the SustainSys library does not rely on an abstraction such
        // as TimeProvider and times need to be current
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);

        // Generate certificates before initialization if needed
        X509Certificate2? signingCertificate = null;
        X509Certificate2? publicCertificate = null;
        if (_shouldGenerateSigningCertificate)
        {
            signingCertificate = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);
            SigningCertificate = signingCertificate; // Expose for tests
            publicCertificate = X509CertificateLoader.LoadCertificate(signingCertificate.Export(X509ContentType.Cert));
        }

        await InitializeIdentityProvider(fakeTimeProvider);

        await InitializeServiceProvider(IdpHost!.Uri(), signingCertificate);

        _serviceProviders.Add(new SamlServiceProvider
        {
            EntityId = "https://localhost:5001/Saml2",
            DisplayName = "Test Service Provider",
            Enabled = true,
            AssertionConsumerServiceUrls = [new Uri($"{SpHost!.Uri()}/Saml2/Acs")],
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            RequireSignedAuthnRequests = publicCertificate != null,
            SigningCertificates = publicCertificate == null ? null : new[] { publicCertificate },
            EncryptionCertificates = publicCertificate == null ? null : new[] { publicCertificate },
            EncryptAssertions = _shouldRequireEncryptedAssertions
        });

        BrowserClient = SpHost.CreateClient();
    }

    private async Task InitializeIdentityProvider(FakeTimeProvider fakeTimeProvider)
    {
        var selfSignedCertificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(SamlFixture.StableSigningCert), null);

        IdpHost = await KestrelTestHost.Create(output,
            services =>
            {
                services.AddSingleton<TimeProvider>(fakeTimeProvider);
                services.AddSingleton<IDistributedCache>(sp => new FakeDistributedCache(sp.GetRequiredService<TimeProvider>()));

                // Register the mutable service provider list before AddSamlServices
                // so TryAdd in AddSamlServices won't overwrite our registration
                services.AddSingleton<ISamlServiceProviderStore>(new InMemorySamlServiceProviderStore(_serviceProviders));

                services.AddIdentityServer(options =>
                    {
                        options.UserInteraction.LoginUrl = "/account/login";
                        options.UserInteraction.LogoutUrl = "/account/logout";
                        options.UserInteraction.ConsentUrl = "/consent";
                        options.KeyManagement.Enabled = false;
                    })
                    .AddSigningCredential(selfSignedCertificate)
                    .AddSamlServices();
            },
            app =>
            {
                app.UseIdentityServer();

                app.MapGet("/account/login", () => Results.Ok());
                app.MapGet("/account/logout", () => Results.Ok());
                app.MapGet("/consent", () => Results.Ok());

                app.MapGet("/__signin", async (HttpContext ctx) =>
                {
                    if (_userToSignIn?.Identity == null)
                    {
                        throw new InvalidOperationException(
                            $"Must set user prior to signin and must have an identity");
                    }

                    await ctx.SignInAsync(_userToSignIn, new AuthenticationProperties());
                    _userToSignIn = null;
                    ctx.Response.StatusCode = 204;
                });

                app.MapGet("/__signout", async ctx =>
                {
                    await ctx.SignOutAsync();
                    ctx.Response.StatusCode = 204;
                });
            },
            CancellationToken.None);
    }

    private async Task InitializeServiceProvider(string identityProviderHostUri, X509Certificate2? signingCertificate = null) => SpHost = await KestrelTestHost.Create(output,
            services =>
            {
                services.AddAuthentication(opt =>
                    {
                        opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        opt.DefaultChallengeScheme = Saml2Defaults.Scheme;
                    })
                    .AddCookie()
                    .AddSaml2(opt =>
                    {
                        opt.SPOptions.EntityId = new Sustainsys.Saml2.Metadata.EntityId("https://localhost:5001/Saml2");
                        opt.SPOptions.WantAssertionsSigned = false;
                        if (signingCertificate != null)
                        {
                            opt.SPOptions.ServiceCertificates.Add(signingCertificate);
                        }

                        opt.IdentityProviders.Add(
                            new IdentityProvider(new Sustainsys.Saml2.Metadata.EntityId(identityProviderHostUri), opt.SPOptions)
                            {
                                LoadMetadata = true,
                                MetadataLocation = $"{identityProviderHostUri}/saml/metadata",
                                SingleSignOnServiceUrl = new Uri($"{identityProviderHostUri}/saml/signin"),
                                WantAuthnRequestsSigned = signingCertificate != null
                            });
                    });
                services.AddAuthorization();
            },
            app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();

                app.MapGet("/protected-resource", () => "Protected Resource").RequireAuthorization();

                app.MapGet("/user-name-identifier", async context =>
                {
                    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userId == null || string.IsNullOrWhiteSpace(userId.Value))
                    {
                        throw new InvalidOperationException("No name identifier claim found for user or claim had no value.");
                    }

                    await context.Response.WriteAsync(userId.Value, context.RequestAborted);
                }).RequireAuthorization();
            },
            CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (SpHost != null)
        {
            await SpHost.DisposeAsync();
        }

        if (IdpHost != null)
        {
            await IdpHost.DisposeAsync();
        }
    }
}

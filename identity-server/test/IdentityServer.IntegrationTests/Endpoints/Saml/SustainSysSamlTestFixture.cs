// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// NOTE: This file requires the Sustainsys.Saml2.AspNetCore2 package to be added to the project.
// Add this to IdentityServer.IntegrationTests.csproj:
// <PackageReference Include="Sustainsys.Saml2.AspNetCore2" />

#nullable enable

using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Sustainsys.Saml2.AspNetCore2;
using IdentityProvider = Sustainsys.Saml2.IdentityProvider;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

internal class SustainSysSamlTestFixture : IAsyncLifetime
{
    public TestFramework.GenericHost? Host = null!;
    public HttpClient? BrowserClient = null!;
    public X509Certificate2? SigningCertificate { get; private set; }

    public Uri IdentityProviderLoginUri => new Uri(new Uri(_samlFixture.Url()), _samlFixture.LoginUrl);

    private readonly SamlFixture _samlFixture = new();
    private bool _shouldGenerateSigningCertificate;
    private bool _shouldRequireEncryptedAssertions;

    public async Task LoginUserAtIdentityProvider()
    {
        _samlFixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id"), new Claim("name", "Test User"), new Claim(JwtClaimTypes.AuthenticationMethod, "urn:oasis:names:tc:SAML:2.0:ac:classes:Password")], "Test"));
        await BrowserClient!.GetAsync($"{_samlFixture.Url()}/__signin", CancellationToken.None);
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
        _samlFixture.Data.FakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);

        // Generate certificates before initialization if needed
        X509Certificate2? signingCertificate = null;
        X509Certificate2? publicCertificate = null;
        if (_shouldGenerateSigningCertificate)
        {
            signingCertificate = SamlTestHelpers.CreateTestSigningCertificate(_samlFixture.Data.FakeTimeProvider);
            SigningCertificate = signingCertificate; // Expose for tests
            publicCertificate = X509CertificateLoader.LoadCertificate(signingCertificate.Export(X509ContentType.Cert));
        }

        // Initialize SP host first so Host is set when creating SP config for IdP
        await InitializeServiceProvider(_samlFixture.Url(), signingCertificate);

        // Configure the service provider with the actual host URI and add it to the SAML fixture
        var serviceProvider = new SamlServiceProvider
        {
            EntityId = "https://localhost:5001/Saml2",
            DisplayName = "Test Service Provider",
            Enabled = true,
            AssertionConsumerServiceUrls = [new Uri($"{Host!.Url()}/Saml2/Acs")],
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            RequireSignedAuthnRequests = publicCertificate != null,
            SigningCertificates = publicCertificate == null ? null : new[] { publicCertificate },
            EncryptionCertificates = publicCertificate == null ? null : new[] { publicCertificate },
            EncryptAssertions = _shouldRequireEncryptedAssertions
        };

        // Note: With InMemorySamlServiceProviderStore, we cannot add SPs after initialization
        // So we need to add it to the fixture before initialization
        _samlFixture.ServiceProviders.Add(serviceProvider);

        // Initialize the SAML fixture first so we can get the IDP URI
        await _samlFixture.InitializeAsync();
    }

    private async Task InitializeServiceProvider(string identityProviderHostUri, X509Certificate2? signingCertificate = null)
    {
        Host = new TestFramework.GenericHost(identityProviderHostUri.Replace("https://server", "https://sp-server"));
        Host.OnConfigureServices += services =>
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
        };

        Host.OnConfigure += app =>
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
        };

        await Host.InitializeAsync();

        BrowserClient = Host.HttpClient;
    }

    public async ValueTask DisposeAsync()
    {
        if (Host != null)
        {
            // GenericHost doesn't implement IAsyncDisposable
            await Task.CompletedTask;
        }

        await _samlFixture.DisposeAsync();
    }
}

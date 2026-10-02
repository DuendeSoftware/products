// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

/// <summary>
/// Verifies that the standalone (static) SAML Service Provider's
/// <see cref="SamlServiceProviderOptions.AuthnRequestSigningBehavior"/> controls whether the
/// emitted HTTP-Redirect binding AuthnRequest is signed with an RSA certificate, that the
/// signature cryptographically verifies against the configured SP certificate, that this
/// parallels the dynamic provider surface, and that SP metadata reflects the configured
/// behavior via AuthnRequestsSigned.
/// </summary>
public sealed class SamlServiceProviderAuthnRequestSigningTests(ITestOutputHelper output)
{
    private const string Category = "Static SAML SP AuthnRequest signing";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static async Task<KestrelTestHost> CreateStaticSpHost(
        ITestOutputHelper output,
        AuthnRequestSigningBehavior behavior,
        X509Certificate2? spCertificate,
        string? outboundSigningAlgorithm,
        Ct ct) =>
        await KestrelTestHost.Create(output,
            services =>
            {
                services.AddIdentityServer();
                services.AddAuthentication().AddSamlServiceProvider(opts =>
                {
                    opts.SpEntityId = "https://static-sp.example.com";
                    opts.IdpEntityId = "https://idp.example.com";
                    opts.SingleSignOnServiceUrl = "https://idp.example.com/sso";
                    opts.AuthnRequestSigningBehavior = behavior;
                    if (spCertificate != null)
                    {
                        opts.SpSigningCertificateBase64 = Convert.ToBase64String(
                            spCertificate.Export(X509ContentType.Pfx));
                    }

                    if (outboundSigningAlgorithm != null)
                    {
                        opts.OutboundSigningAlgorithm = outboundSigningAlgorithm;
                    }
                });
            },
            app =>
            {
                app.MapGet("/login", async (HttpContext ctx) =>
                {
                    await ctx.ChallengeAsync(SamlServiceProviderDefaults.Scheme, new AuthenticationProperties { RedirectUri = "/" });
                });
            },
            ct);

    private static async Task<Uri> GetChallengeRedirectLocationAsync(KestrelTestHost host, Ct ct)
    {
        using var client = host.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync("/login", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        var location = response.Headers.Location;
        location.ShouldNotBeNull();
        return location;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task always_signs_authn_request_redirect_with_rsa_certificate_and_signature_verifies()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);

        await using var host = await CreateStaticSpHost(output, AuthnRequestSigningBehavior.Always, spCert, outboundSigningAlgorithm: null, _ct);

        var location = await GetChallengeRedirectLocationAsync(host, _ct);

        var sigAlg = Uri.UnescapeDataString(SamlTestHelpers.ExtractRawQueryParam(location.Query, "SigAlg")!);
        sigAlg.ShouldBe("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");

        SamlTestHelpers.VerifyRedirectBindingSignature(location, spCert).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task never_does_not_sign_authn_request_redirect()
    {
        await using var host = await CreateStaticSpHost(output, AuthnRequestSigningBehavior.Never, spCertificate: null, outboundSigningAlgorithm: null, _ct);

        var location = await GetChallengeRedirectLocationAsync(host, _ct);

        SamlTestHelpers.ExtractRawQueryParam(location.Query, "SigAlg").ShouldBeNull();
        SamlTestHelpers.ExtractRawQueryParam(location.Query, "Signature").ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task static_and_dynamic_surfaces_produce_equivalent_signing_behavior_for_always()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);
        var spCertBase64 = Convert.ToBase64String(spCert.Export(X509ContentType.Pfx));

        await using var staticHost = await CreateStaticSpHost(output, AuthnRequestSigningBehavior.Always, spCert, outboundSigningAlgorithm: null, _ct);
        var staticLocation = await GetChallengeRedirectLocationAsync(staticHost, _ct);

        await using var dynamicFixture = new SamlDynamicProviderFixture(output,
            configureSamlProvider: provider =>
            {
                provider.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
                provider.SpSigningCertificateBase64 = spCertBase64;
            });
        await dynamicFixture.InitializeAsync();
        var dynamicLocation = await dynamicFixture.GetChallengeRedirectLocationAsync();

        // Both surfaces must sign, use the same SigAlg, and verify against the same certificate.
        var staticSigAlg = Uri.UnescapeDataString(SamlTestHelpers.ExtractRawQueryParam(staticLocation.Query, "SigAlg")!);
        var dynamicSigAlg = Uri.UnescapeDataString(SamlTestHelpers.ExtractRawQueryParam(dynamicLocation.Query, "SigAlg")!);
        staticSigAlg.ShouldBe(dynamicSigAlg);

        SamlTestHelpers.VerifyRedirectBindingSignature(staticLocation, spCert).ShouldBeTrue();
        SamlTestHelpers.VerifyRedirectBindingSignature(dynamicLocation, spCert).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task static_and_dynamic_surfaces_produce_equivalent_signing_behavior_for_never()
    {
        await using var staticHost = await CreateStaticSpHost(output, AuthnRequestSigningBehavior.Never, spCertificate: null, outboundSigningAlgorithm: null, _ct);
        var staticLocation = await GetChallengeRedirectLocationAsync(staticHost, _ct);

        await using var dynamicFixture = new SamlDynamicProviderFixture(output,
            configureSamlProvider: provider =>
            {
                provider.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Never;
            });
        await dynamicFixture.InitializeAsync();
        var dynamicLocation = await dynamicFixture.GetChallengeRedirectLocationAsync();

        SamlTestHelpers.ExtractRawQueryParam(staticLocation.Query, "SigAlg").ShouldBeNull();
        SamlTestHelpers.ExtractRawQueryParam(dynamicLocation.Query, "SigAlg").ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_includes_authn_requests_signed_true_when_always()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);

        await using var host = await CreateStaticSpHost(output, AuthnRequestSigningBehavior.Always, spCert, outboundSigningAlgorithm: null, _ct);

        using var client = host.CreateClient();
        var result = await client.GetAsync("/Saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);
        var md = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");

        var spDescriptor = doc.Descendants(md + "SPSSODescriptor").Single();
        spDescriptor.Attribute("AuthnRequestsSigned")
            .ShouldNotBeNull()
            .Value
            .ShouldBe("true");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_does_not_include_authn_requests_signed_true_when_never()
    {
        await using var host = await CreateStaticSpHost(output, AuthnRequestSigningBehavior.Never, spCertificate: null, outboundSigningAlgorithm: null, _ct);

        using var client = host.CreateClient();
        var result = await client.GetAsync("/Saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);
        var md = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");

        var spDescriptor = doc.Descendants(md + "SPSSODescriptor").Single();
        var attribute = spDescriptor.Attribute("AuthnRequestsSigned");
        if (attribute != null)
        {
            attribute.Value.ShouldBe("false");
        }
    }
}

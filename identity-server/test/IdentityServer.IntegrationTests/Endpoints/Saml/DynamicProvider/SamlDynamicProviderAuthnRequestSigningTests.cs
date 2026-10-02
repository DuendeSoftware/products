// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

/// <summary>
/// Verifies that the dynamic SAML provider's <see cref="AuthnRequestSigningBehavior"/> controls
/// whether the emitted HTTP-Redirect binding AuthnRequest is signed with an RSA certificate,
/// that the signature cryptographically verifies against the configured SP certificate, and
/// that the setting survives round-tripping through the EF configuration store and identity
/// provider cache.
/// </summary>
public class SamlDynamicProviderAuthnRequestSigningTests(ITestOutputHelper output)
{
    private const string Category = "Dynamic SAML provider AuthnRequest signing";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task always_signs_authn_request_redirect_with_rsa_certificate_and_signature_verifies()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);
        var spCertBase64 = Convert.ToBase64String(spCert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));

        await using var fixture = new SamlDynamicProviderFixture(output,
            configureSamlProvider: provider =>
            {
                provider.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
                provider.SpSigningCertificateBase64 = spCertBase64;
            });
        await fixture.InitializeAsync();

        var location = await fixture.GetChallengeRedirectLocationAsync();

        var query = location.Query;
        SamlTestHelpers.ExtractRawQueryParam(query, "SigAlg").ShouldNotBeNull();
        var sigAlg = Uri.UnescapeDataString(SamlTestHelpers.ExtractRawQueryParam(query, "SigAlg")!);
        sigAlg.ShouldBe("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");

        SamlTestHelpers.VerifyRedirectBindingSignature(location, spCert).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task never_does_not_sign_authn_request_redirect()
    {
        await using var fixture = new SamlDynamicProviderFixture(output,
            configureSamlProvider: provider =>
            {
                provider.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Never;
            });
        await fixture.InitializeAsync();

        var location = await fixture.GetChallengeRedirectLocationAsync();

        SamlTestHelpers.ExtractRawQueryParam(location.Query, "SigAlg").ShouldBeNull();
        SamlTestHelpers.ExtractRawQueryParam(location.Query, "Signature").ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task authn_request_signing_behavior_survives_store_and_cache_round_trip()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);
        var spCertBase64 = Convert.ToBase64String(spCert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));

        await using var fixture = new SamlDynamicProviderFixture(output,
            configureSamlProvider: provider =>
            {
                provider.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
                provider.SpSigningCertificateBase64 = spCertBase64;
            });
        await fixture.InitializeAsync();

        // First fetch goes through the EF store; a second fetch may be served from cache
        // (depending on IdentityServerOptions.Caching configuration). Either way, the value
        // must be the same as what was seeded.
        var first = await fixture.ReloadSamlProviderFromStoreAsync();
        var second = await fixture.ReloadSamlProviderFromStoreAsync();

        first.AuthnRequestSigningBehavior.ShouldBe(AuthnRequestSigningBehavior.Always);
        second.AuthnRequestSigningBehavior.ShouldBe(AuthnRequestSigningBehavior.Always);
        first.SpSigningCertificateBase64.ShouldBe(spCertBase64);
        second.SpSigningCertificateBase64.ShouldBe(spCertBase64);

        // And the round-tripped configuration must still produce a signed, verifiable redirect.
        var location = await fixture.GetChallengeRedirectLocationAsync();
        SamlTestHelpers.VerifyRedirectBindingSignature(location, spCert).ShouldBeTrue();
    }
}

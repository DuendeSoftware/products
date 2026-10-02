// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Cryptography.X509Certificates;
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
/// Verifies that AuthnRequests emitted over the HTTP-POST binding are cryptographically signed
/// and verifiable using RSA SP signing certificates, on both the standalone (static) SAML
/// Service Provider surface and the dynamic provider surface. This covers the internal
/// <c>Saml2PostBinding</c>/<c>XmlHelpers.Sign</c> path, which is distinct from the HTTP-Redirect
/// binding path exercised by <see cref="SamlServiceProviderAuthnRequestSigningTests"/>.
/// </summary>
public sealed class SamlServiceProviderAuthnRequestPostBindingSigningTests(ITestOutputHelper output)
{
    private const string Category = "SAML SP AuthnRequest HTTP-POST binding signing";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static async Task<KestrelTestHost> CreateStaticSpHost(
        ITestOutputHelper output,
        X509Certificate2 spCertificate,
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
                    opts.BindingType = SamlBindingType.HttpPost;
                    opts.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
                    opts.SpSigningCertificateBase64 = Convert.ToBase64String(
                        spCertificate.Export(X509ContentType.Pfx));

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

    private static async Task<HttpResponseMessage> GetChallengePostFormResponseAsync(KestrelTestHost host, Ct ct)
    {
        using var client = host.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync("/login", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return response;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task static_sp_post_binding_signs_authn_request_with_rsa_certificate_and_signature_verifies()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);

        await using var host = await CreateStaticSpHost(output, spCert, outboundSigningAlgorithm: null, _ct);

        using var response = await GetChallengePostFormResponseAsync(host, _ct);
        var (xml, _, _) = await SamlTestHelpers.ExtractSamlPostMessageAsync(response, "SAMLRequest", _ct);

        SamlTestHelpers.VerifyPostBindingSignature(
                xml,
                spCert,
                expectedSignatureMethod: "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
                expectedDigestMethod: "http://www.w3.org/2001/04/xmlenc#sha256")
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dynamic_provider_post_binding_signs_authn_request_with_rsa_certificate_and_signature_verifies()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(fakeTimeProvider);
        var spCertBase64 = Convert.ToBase64String(spCert.Export(X509ContentType.Pfx));

        await using var fixture = new SamlDynamicProviderFixture(output,
            configureSamlProvider: provider =>
            {
                provider.AuthnRequestSigningBehavior = AuthnRequestSigningBehavior.Always;
                provider.SpSigningCertificateBase64 = spCertBase64;
                provider.BindingType = "post";
            });
        await fixture.InitializeAsync();

        using var client = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        using var response = await client.GetAsync("/account/login?ReturnUrl=/", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (xml, _, _) = await SamlTestHelpers.ExtractSamlPostMessageAsync(response, "SAMLRequest", _ct);

        SamlTestHelpers.VerifyPostBindingSignature(
                xml,
                spCert,
                expectedSignatureMethod: "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
                expectedDigestMethod: "http://www.w3.org/2001/04/xmlenc#sha256")
            .ShouldBeTrue();
    }
}

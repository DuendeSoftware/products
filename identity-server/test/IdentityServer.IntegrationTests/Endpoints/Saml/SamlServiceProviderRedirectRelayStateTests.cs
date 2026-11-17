// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Sp.Exceptions;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Saml.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Saml2HandlerOptions = Duende.IdentityServer.Internal.Saml.Sp.AspNetCore.Saml2Options;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

/// <summary>
/// Covers the standalone (static) <c>AddSamlServiceProvider()</c> surface's handling of the
/// RelayState query string parameter on an incoming, HTTP-Redirect-bound LogoutRequest,
/// parallel to <see cref="DynamicProvider.SamlSpRedirectRelayStateTests" /> for the dynamic
/// provider surface.
/// </summary>
public sealed class SamlServiceProviderRedirectRelayStateTests(ITestOutputHelper output)
{
    private const string Category = "Static SAML SP redirect binding RelayState";
    private const string SigAlg = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string LogoutPath = $"{SamlServiceProviderDefaults.ModulePath}/Logout";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_unchanged_relay_state_is_accepted()
    {
        var (idpCert, spCert) = CreateCertificates();
        var recorder = new ExceptionRecorder();
        var capturedRelayState = new CapturedRelayState();
        await using var host = await CreateHost(idpCert, spCert, recorder, capturedRelayState);

        const string relayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml();
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, relayState, idpCert, SigAlg);

        var query =
            $"SAMLRequest={encoded}" +
            $"&RelayState={Uri.EscapeDataString(relayState)}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        using var client = host.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync($"{LogoutPath}?{query}", _ct);

        recorder.Exception.ShouldBeNull("an unchanged, correctly signed RelayState must be accepted");
        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther,
            "without a session, the SP answers the IdP with a LogoutResponse redirect");
        capturedRelayState.Value.ShouldBe(relayState);
        System.Web.HttpUtility.ParseQueryString(response.Headers.Location!.Query)["RelayState"]
            .ShouldBe(relayState, "the LogoutResponse must carry the RelayState the IdP signed");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_unchanged_relay_state_but_broken_signature_is_rejected()
    {
        // Sanity check for the positive test above: breaking the signature (one flipped
        // character) over an otherwise identical, unchanged RelayState must now be rejected,
        // proving the positive test's 200 OK / captured RelayState actually depend on signature
        // verification passing, not on some other code path.
        var (idpCert, spCert) = CreateCertificates();
        var recorder = new ExceptionRecorder();
        var capturedRelayState = new CapturedRelayState();
        await using var host = await CreateHost(idpCert, spCert, recorder, capturedRelayState);

        const string relayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml();
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, relayState, idpCert, SigAlg);
        var brokenSignature = BreakBase64Signature(signature);

        var query =
            $"SAMLRequest={encoded}" +
            $"&RelayState={Uri.EscapeDataString(relayState)}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(brokenSignature)}";

        using var client = host.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync($"{LogoutPath}?{query}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a broken signature over an otherwise unchanged RelayState must be rejected by the exception recorder");
        AssertExceptionType<InvalidSignatureException>(recorder);
        capturedRelayState.Value.ShouldBeNull("the request must be rejected before a LogoutResponse is built");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_bare_equals_sign_in_relay_state_is_rejected()
    {
        var (idpCert, spCert) = CreateCertificates();
        var recorder = new ExceptionRecorder();
        var capturedRelayState = new CapturedRelayState();
        await using var host = await CreateHost(idpCert, spCert, recorder, capturedRelayState);

        // The signature is computed over the signed RelayState value "abc123", exactly as a
        // genuine IdP would sign it.
        const string signedRelayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml();
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, signedRelayState, idpCert, SigAlg);

        // The raw query's RelayState value is then changed, after signing, by appending "="
        // followed by URL-encoded text, while keeping the signed, unmodified prefix intact.
        const string appendedRaw = "=%26returnUrl%3Dhttps%3A%2F%2Fother.example";

        var query =
            $"SAMLRequest={encoded}" +
            $"&RelayState={signedRelayState}{appendedRaw}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        using var client = host.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync($"{LogoutPath}?{query}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a RelayState value with text after a bare '=' must not pass signature verification " +
            "for a RelayState value that was not actually signed");
        AssertExceptionType<InvalidSignatureException>(recorder);
        capturedRelayState.Value.ShouldBeNull("the request must be rejected before a LogoutResponse is built");
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("SAMLRequest")]
    [InlineData("SAMLResponse")]
    [InlineData("RelayState")]
    [InlineData("SigAlg")]
    [InlineData("Signature")]
    public async Task logout_request_with_duplicate_parameter_is_rejected(string parameterName)
    {
        var (idpCert, spCert) = CreateCertificates();
        var recorder = new ExceptionRecorder();
        var capturedRelayState = new CapturedRelayState();
        await using var host = await CreateHost(idpCert, spCert, recorder, capturedRelayState);

        const string relayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml();
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, relayState, idpCert, SigAlg);

        var parameters = new List<(string Name, string Value)>
        {
            ("SAMLRequest", encoded),
            ("RelayState", Uri.EscapeDataString(relayState)),
            ("SigAlg", Uri.EscapeDataString(sigAlg)),
            ("Signature", Uri.EscapeDataString(signature)),
        };
        if (parameterName == "SAMLResponse")
        {
            parameters.Add(("SAMLResponse", "first"));
            parameters.Add(("SAMLResponse", "second"));
        }
        else
        {
            parameters.Add(parameters.Single(p => p.Name == parameterName));
        }

        var query = string.Join("&", parameters.Select(p => $"{p.Name}={p.Value}"));

        using var client = host.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync($"{LogoutPath}?{query}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            $"a query string with a duplicate {parameterName} parameter must not be processed as a logout");
        AssertExceptionType<BadFormatSamlResponseException>(recorder);
        capturedRelayState.Value.ShouldBeNull("the request must be rejected before a LogoutResponse is built");
    }

    private static (X509Certificate2 idpCert, X509Certificate2 spCert) CreateCertificates()
    {
        var timeProvider = TimeProvider.System;
        var idpCert = SamlTestHelpers.CreateTestSigningCertificate(timeProvider, "CN=Static SP Test Idp Signing");
        var spCert = SamlTestHelpers.CreateTestSigningCertificate(timeProvider, "CN=Static SP Test Sp Signing");
        return (idpCert, spCert);
    }

    // Flips one byte in the decoded signature while keeping it valid Base64, so a correctly
    // formed but cryptographically wrong signature is sent - proving that it is the signature
    // check, not merely well-formedness, that the positive test's assertions depend on.
    private static string BreakBase64Signature(string base64Signature)
    {
        var bytes = Convert.FromBase64String(base64Signature);
        bytes[0] ^= 0xFF;
        return Convert.ToBase64String(bytes);
    }

    private async Task<KestrelTestHost> CreateHost(
        X509Certificate2 idpCert, X509Certificate2 spCert, ExceptionRecorder recorder, CapturedRelayState capturedRelayState) =>
        await KestrelTestHost.Create(output,
            services =>
            {
                services.AddSingleton<IStartupFilter>(recorder);
                services.AddIdentityServer()
                    .AddDeveloperSigningCredential(persistKey: false);

                // A cookie scheme is configured as the default sign-in/sign-out scheme so that
                // LogOutCommand's TerminateLocalSession redirect-free sign-out has a well
                // defined handler, giving a deterministic 200 OK on acceptance instead of
                // falling back to signing out of the SAML scheme itself.
                services.AddAuthentication(options =>
                    {
                        options.DefaultSignInScheme = "sp-cookie";
                        options.DefaultSignOutScheme = "sp-cookie";
                    })
                    .AddCookie("sp-cookie")
                    .AddSamlServiceProvider(options =>
                    {
                        options.SpEntityId = "https://static-sp.example.com";
                        options.IdpEntityId = "https://static-idp.example.com";
                        options.SingleSignOnServiceUrl = "https://static-idp.example.com/Saml2/SSO";
                        options.SingleLogoutServiceUrl = "https://static-idp.example.com/Saml2/SLO";
                        options.SigningCertificatesBase64.Add(Convert.ToBase64String(idpCert.Export(X509ContentType.Cert)));
                        options.SpSigningCertificateBase64 = Convert.ToBase64String(spCert.Export(X509ContentType.Pfx));
                    });

                services.AddSingleton<IConfigureOptions<Saml2HandlerOptions>>(new ConfigureRelayStateCapture(capturedRelayState));
            },
            _ => { },
            _ct);

    // Some exceptions reach the handler wrapped (e.g. by the SAML library's own pipeline
    // code); assert on the specific inner exception in that case, rather than accepting any
    // exception via a loose fallback.
    private static void AssertExceptionType<TException>(ExceptionRecorder recorder) where TException : Exception
    {
        recorder.Exception.ShouldNotBeNull("the exception recorder must have captured an exception");
        var actual = recorder.Exception is TException ? recorder.Exception : recorder.Exception.InnerException;
        actual.ShouldBeOfType<TException>(
            $"expected {typeof(TException).Name}, but the recorder captured {recorder.Exception.GetType().FullName}: {recorder.Exception}");
    }

    private sealed class CapturedRelayState
    {
        public string? Value { get; set; }
    }

    private sealed class ConfigureRelayStateCapture(CapturedRelayState captured) : IConfigureNamedOptions<Saml2HandlerOptions>
    {
        public void Configure(Saml2HandlerOptions options)
        {
        }

        public void Configure(string? name, Saml2HandlerOptions options)
        {
            var existing = options.Notifications.LogoutResponseCreated;
            options.Notifications.LogoutResponseCreated = (response, request, user, idp) =>
            {
                existing?.Invoke(response, request, user, idp);
                captured.Value = response.RelayState;
            };
        }
    }

    private static string BuildLogoutRequestXml() =>
        $"""
         <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                              xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                              ID="_{Guid.NewGuid():N}" Version="2.0"
                              IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"
                              Destination="https://static-sp.example.com{LogoutPath}">
           <saml:Issuer>https://static-idp.example.com</saml:Issuer>
           <saml:NameID>test-user</saml:NameID>
         </samlp:LogoutRequest>
         """;
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Duende.IdentityServer.Hosting.FederatedSignOut;
using Duende.IdentityServer.Internal.Saml.Sp.Exceptions;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

/// <summary>
/// Covers the SAML SP HTTP-Redirect binding's signature verification
/// (<see cref="Internal.Saml.Sp.Bindings.Saml2RedirectBinding" />.CheckSignature) for an
/// IdP-initiated LogoutRequest, in particular how the RelayState query string parameter is
/// parsed and carried through to the outbound, IdentityServer-signed LogoutResponse.
///
/// <see cref="Internal.Saml.Sp.Commands.LogoutCommand" />.HandleRequest copies
/// <c>unbindResult.RelayState</c> into the outbound LogoutResponse, and
/// <see cref="AuthenticationRequestHandlerWrapper" /> copies the same value into the stored
/// <see cref="SamlSpLogoutMessage" />, which flows to the SP logout completion endpoint. The
/// completion endpoint signs the outbound LogoutResponse (including the RelayState) with
/// IdentityServer's own signing key on the HTTP-Redirect binding.
/// </summary>
public class SamlSpRedirectRelayStateTests(ITestOutputHelper output)
{
    private const string Category = "SAML SP redirect binding RelayState";
    private const string SigAlg = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string GenuineScheme = "saml-idp";
    private const string GenuineLogoutPath = "/federation/saml-idp/Saml2/Logout";
    private const string CompletionPath = "/saml/slo/sp-complete";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task control_properly_signed_logout_request_with_relay_state_is_accepted_and_relay_state_is_echoed_back()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        const string relayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, relayState, idpSigningCert, SigAlg);

        var query =
            $"SAMLRequest={encoded}" +
            $"&RelayState={Uri.EscapeDataString(relayState)}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        var controlResponse = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{query}", _ct);

        // The properly signed request is accepted (signature check passes) and processed as a
        // logout, i.e. redirected to the SP's own logout-completion endpoint.
        recorder.Exception.ShouldBeNull("a properly signed request must be accepted without an exception");
        controlResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var completionLocation = controlResponse.Headers.Location;
        completionLocation.ShouldNotBeNull();
        completionLocation.AbsolutePath.ShouldBe(CompletionPath);

        var logoutId = System.Web.HttpUtility.ParseQueryString(completionLocation.Query)["logoutId"];
        logoutId.ShouldNotBeNullOrWhiteSpace();

        await using var scope = fixture.SpHost!.ConfiguredServices.CreateAsyncScope();
        var messageStore = scope.ServiceProvider.GetRequiredService<IMessageStore<SamlSpLogoutMessage>>();
        var storedMessage = await messageStore.ReadAsync(logoutId!, _ct);
        storedMessage.Data.RelayState.ShouldBe(relayState);

        // Follow to the completion endpoint: the outbound, IdentityServer-signed LogoutResponse
        // carries the unmodified RelayState back to the IdP.
        var completionResponse = await sharedCookieClient.GetAsync(
            completionLocation.PathAndQuery, _ct);
        completionResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect, recorder.Exception?.ToString() ?? "no exception recorded");
        var logoutResponseData = SamlTestHelpers.ExtractSamlLogoutResponseFromRedirect(completionResponse);
        logoutResponseData.RelayState.ShouldBe(relayState);

        var completionQuery = completionResponse.Headers.Location!.Query;
        SamlTestHelpers.ExtractRawQueryParam(completionQuery, "SigAlg").ShouldNotBeNullOrWhiteSpace();
        SamlTestHelpers.ExtractRawQueryParam(completionQuery, "Signature").ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("a%3Db")]
    [InlineData("a=b")]
    [InlineData("next%3D%2Fhome%3Fx%3D1%26y%3D2")]
    public async Task signed_relay_state_containing_an_equals_sign_is_accepted_and_echoed_back(string rawRelayState)
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var signedText = $"SAMLRequest={encoded}&RelayState={rawRelayState}&SigAlg={Uri.EscapeDataString(SigAlg)}";
        using var rsa = idpSigningCert.GetRSAPrivateKey()!;
        var signature = Convert.ToBase64String(
            rsa.SignData(Encoding.UTF8.GetBytes(signedText), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        var response = await sharedCookieClient.GetAsync(
            $"{GenuineLogoutPath}?{signedText}&Signature={Uri.EscapeDataString(signature)}", _ct);

        recorder.Exception.ShouldBeNull($"a RelayState the IdP signed as '{rawRelayState}' must be accepted");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.ShouldBe(CompletionPath);

        var completionResponse = await sharedCookieClient.GetAsync(response.Headers.Location.PathAndQuery, _ct);
        completionResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect, recorder.Exception?.ToString() ?? "no exception recorded");
        SamlTestHelpers.ExtractSamlLogoutResponseFromRedirect(completionResponse).RelayState
            .ShouldBe(Uri.UnescapeDataString(rawRelayState), "the IdP must get back the RelayState it sent, decoded");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_bare_equals_sign_in_relay_state_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        // The signature is computed over the signed RelayState value "abc123", exactly as a
        // genuine IdP would sign it.
        const string signedRelayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, signedRelayState, idpSigningCert, SigAlg);

        // The raw query's RelayState value is then changed, after signing, by appending "="
        // followed by URL-encoded text, while keeping the signed, unmodified prefix intact.
        const string appendedRaw = "=%26returnUrl%3Dhttps%3A%2F%2Fother.example";

        var changedQuery =
            $"SAMLRequest={encoded}" +
            $"&RelayState={signedRelayState}{appendedRaw}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        var changedResponse = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{changedQuery}", _ct);

        // The request must not be accepted as a logout: it is not redirected to the SP's
        // completion endpoint.
        changedResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a RelayState value with text after a bare '=' must not pass signature verification " +
            "for a RelayState value that was not actually signed");
        AssertExceptionType<InvalidSignatureException>(recorder);

        // The session is not terminated by the rejected request.
        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task control_changing_relay_state_without_an_extra_equals_sign_fails_signature_verification()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        const string signedRelayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, signedRelayState, idpSigningCert, SigAlg);

        // Change the RelayState to a different value *without* using a raw '=' character -
        // a plain substitution, not an append-after-'=' trick. This must fail signature
        // validation, because the signed text now includes a different RelayState value
        // than what was actually signed.
        const string differentRelayState = "abc124";

        var changedQuery =
            $"SAMLRequest={encoded}" +
            $"&RelayState={differentRelayState}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{changedQuery}", _ct);

        // The signature no longer validates, so this must not be processed as a successful,
        // accepted logout redirect.
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a signed RelayState value that is substituted with a different value must fail signature verification");
        AssertExceptionType<InvalidSignatureException>(recorder);

        // Control: the session remains untouched by the rejected request, showing the
        // signature check is genuinely active (not a no-op) for this endpoint/scheme.
        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_duplicate_saml_request_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        const string signedRelayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, signedRelayState, idpSigningCert, SigAlg);

        var duplicateQuery =
            $"SAMLRequest={encoded}" +
            $"&SAMLRequest={encoded}" +
            $"&RelayState={signedRelayState}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{duplicateQuery}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a query string with a duplicate SAMLRequest parameter must not be processed as a logout");
        AssertExceptionType<BadFormatSamlResponseException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_response_with_duplicate_saml_response_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        var duplicateQuery = "SAMLResponse=first&SAMLResponse=second";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{duplicateQuery}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a query string with a duplicate SAMLResponse parameter must not be processed as a logout");
        AssertExceptionType<BadFormatSamlResponseException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_sig_alg_and_no_signature_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);

        var query =
            $"SAMLRequest={encoded}" +
            "&RelayState=abc123" +
            $"&SigAlg={Uri.EscapeDataString(SigAlg)}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{query}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a query string with SigAlg but no Signature must not be processed as a logout");
        AssertExceptionType<InvalidSignatureException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_duplicate_relay_state_is_rejected_and_session_stays_authenticated()

    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        const string signedRelayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, signedRelayState, idpSigningCert, SigAlg);

        // Two RelayState parameters, both equal to the signed value individually - still must
        // be rejected, because which one is authoritative is ambiguous.
        var duplicateQuery =
            $"SAMLRequest={encoded}" +
            $"&RelayState={signedRelayState}" +
            $"&RelayState={signedRelayState}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{duplicateQuery}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a query string with a duplicate RelayState parameter must not be processed as a logout");
        AssertExceptionType<BadFormatSamlResponseException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_duplicate_sig_alg_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        const string signedRelayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, signedRelayState, idpSigningCert, SigAlg);

        var duplicateQuery =
            $"SAMLRequest={encoded}" +
            $"&RelayState={signedRelayState}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{duplicateQuery}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a query string with a duplicate SigAlg parameter must not be processed as a logout");
        AssertExceptionType<BadFormatSamlResponseException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_request_with_duplicate_signature_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        const string signedRelayState = "abc123";
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, signedRelayState, idpSigningCert, SigAlg);

        var duplicateQuery =
            $"SAMLRequest={encoded}" +
            $"&RelayState={signedRelayState}" +
            $"&SigAlg={Uri.EscapeDataString(sigAlg)}" +
            $"&Signature={Uri.EscapeDataString(signature)}" +
            $"&Signature={Uri.EscapeDataString(signature)}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{duplicateQuery}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a query string with a duplicate Signature parameter must not be processed as a logout");
        AssertExceptionType<BadFormatSamlResponseException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task xml_signed_logout_request_with_relay_state_and_no_query_string_signature_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        // The message itself carries a valid, enveloped XML signature, but the query string
        // has no SigAlg/Signature at all, so RelayState - which is never part of the XML
        // signature - is carried by a completely unsigned channel.
        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var signedXml = SamlTestHelpers.SignAuthNRequestXml(logoutRequestXml, idpSigningCert);
        var encoded = await SamlTestHelpers.EncodeRequest(signedXml, _ct);

        var query = $"SAMLRequest={encoded}&RelayState={Uri.EscapeDataString("https://other.example/x")}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{query}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a RelayState that is not covered by any query string signature must not be accepted on an " +
            "HTTP-Redirect LogoutRequest, even when the message itself carries a valid XML signature");
        AssertExceptionType<InvalidSignatureException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task xml_signed_logout_request_without_relay_state_and_no_query_string_signature_is_accepted()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();
        var idpSigningCert = LoadGenuineIdpSigningCertificate();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var signedXml = SamlTestHelpers.SignAuthNRequestXml(logoutRequestXml, idpSigningCert);
        var encoded = await SamlTestHelpers.EncodeRequest(signedXml, _ct);

        var query = $"SAMLRequest={encoded}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{query}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect, recorder.Exception?.ToString() ?? "no exception recorded");
        recorder.Exception.ShouldBeNull("a validly signed LogoutRequest must be accepted without an exception");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unsigned_logout_request_with_no_query_string_signature_is_rejected_and_session_stays_authenticated()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = BuildFixtureWithExceptionCapturing(recorder);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();

        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);
        var ssoResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");
        ssoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var sharedCookieClient = CreateSharedCookieClient(fixture, spUri);

        var before = await WhoAmIAsync(sharedCookieClient);
        before.authenticated.ShouldBeTrue();

        var logoutRequestXml = BuildLogoutRequestXml(
            issuer: $"{idpUri}/Saml2",
            destination: $"{spUri}{GenuineLogoutPath}",
            nameId: SamlDynamicProviderFixture.TestUserSub);
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);

        var query = $"SAMLRequest={encoded}&RelayState={Uri.EscapeDataString("https://other.example/x")}";

        var response = await sharedCookieClient.GetAsync($"{GenuineLogoutPath}?{query}", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError,
            "a completely unsigned LogoutRequest (no XML signature, no query string signature) must be rejected");
        AssertExceptionType<UnsuccessfulSamlOperationException>(recorder);

        var after = await WhoAmIAsync(sharedCookieClient);
        after.authenticated.ShouldBeTrue();
    }

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

    // The genuine "saml-idp" provider seeded by SamlDynamicProviderFixture does not configure a
    // SingleLogoutServiceUrl or an SP signing certificate by default (it's only used for SSO in
    // the shared fixture tests), so IdP-initiated LogoutRequest handling would otherwise fail
    // before even reaching CheckSignature. Configure both so LogOutCommand.HandleRequest can
    // build and sign an outbound LogoutResponse. The SP host also needs an X509-backed
    // IdentityServer signing credential (the shared fixture registers an RSA-only developer
    // credential, which SamlSigningService cannot wrap as an X509 certificate for outbound
    // LogoutResponse signing on the HTTP-Redirect binding).
    private SamlDynamicProviderFixture BuildFixtureWithExceptionCapturing(ExceptionRecorder recorder)
    {
        var spSigningCert = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=SP IdentityServer Signing");
        return new SamlDynamicProviderFixture(output,
            additionalSpServices: services => services.AddSingleton<IStartupFilter>(recorder),
            configureSamlProvider: provider =>
            {
                provider.SingleLogoutServiceUrl ??= "https://genuine-idp.example.com/Saml2/SLO";
                provider.SpSigningCertificateBase64 ??= Convert.ToBase64String(
                    SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=Genuine SP Signing").Export(X509ContentType.Pfx));
            },
            spIdentityServerSigningCertificate: spSigningCert);
    }

    private static X509Certificate2 LoadGenuineIdpSigningCertificate() =>
        X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(SamlFixture.StableSigningCert), null);

    private static HttpClient CreateSharedCookieClient(SamlDynamicProviderFixture fixture, string spUri) =>
        new(new CookieHandler(new HttpClientHandler { AllowAutoRedirect = false }, fixture.BrowserCookieContainer!))
        {
            BaseAddress = new Uri(spUri)
        };

    private static async Task<(bool authenticated, string? sub, string? idp)> WhoAmIAsync(HttpClient client)
    {
        var response = await client.GetAsync("/__whoami", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        return (body!.authenticated, body.sub, body.idp);
    }

    private sealed record WhoAmIResponse(bool authenticated, string? sub, string? idp);

    private static string BuildLogoutRequestXml(string issuer, string destination, string nameId) =>
        $"""
         <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                              xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                              ID="_{Guid.NewGuid():N}" Version="2.0"
                              IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"
                              Destination="{destination}">
           <saml:Issuer>{issuer}</saml:Issuer>
           <saml:NameID>{nameId}</saml:NameID>
         </samlp:LogoutRequest>
         """;
}

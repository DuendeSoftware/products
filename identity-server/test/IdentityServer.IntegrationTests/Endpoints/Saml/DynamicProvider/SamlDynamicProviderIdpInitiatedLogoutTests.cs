// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

public class SamlDynamicProviderIdpInitiatedLogoutTests(ITestOutputHelper output)
{
    private const string Category = "Dynamic SAML provider IdP-initiated logout";
    private const string SigAlg = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string ContosoScheme = "saml-idp";
    private const string MalloryScheme = "mallory-idp";
    private const string MalloryEntityId = "https://mallory.example/Saml2";
    private const string FabrikamEntityId = "https://fabrikam.example/Saml2";
    private const string SamlAssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";

    private static readonly X509Certificate2 ContosoSigningKey =
        X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(SamlFixture.StableSigningCert), null);

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_signing_idp_logs_out_the_session_user_with_the_session_index_expect_the_session_to_end()
    {
        await using var fixture = await CreateFixtureAsync();
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey,
            aliceSignIn.Sub, aliceSignIn.SessionIndex);

        (await HasSessionAsync(fixture)).ShouldBeFalse("Contoso's IdP signed a LogoutRequest for Alice and her SessionIndex, so Alice's session must end");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_signing_idp_logs_out_the_session_user_without_a_session_index_expect_the_session_to_end()
    {
        await using var fixture = await CreateFixtureAsync();
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey,
            aliceSignIn.Sub, sessionIndex: null);

        (await HasSessionAsync(fixture)).ShouldBeFalse("a LogoutRequest without SessionIndex ends every session of the named user (SAML Core 3.7.3.1), so Alice's session must end");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_a_logout_request_arrives_without_a_session_expect_a_logout_response_to_the_idp()
    {
        await using var fixture = await CreateFixtureAsync();

        var logoutResponse = await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey,
            "any-user", "any-session-index");

        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.SeeOther, "without a session there is nothing to end, so the IdP gets its LogoutResponse at once");
        logoutResponse.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe($"{ContosoEntityId(fixture)}/SLO");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_another_tenants_idp_claims_contosos_entity_id_and_signs_a_logout_request_expect_the_session_to_stay()
    {
        using var mallorySigningKey = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=Mallory IdP");
        await using var fixture = await CreateFixtureAsync();
        await AddMalloryIdpAsync(fixture, mallorySigningKey, malloryIdpEntityId: ContosoEntityId(fixture));
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        await SendLogoutRequestAsync(fixture, MalloryScheme, ContosoEntityId(fixture), mallorySigningKey,
            aliceSignIn.Sub, aliceSignIn.SessionIndex);

        (await HasSessionAsync(fixture)).ShouldBeTrue("Alice signed in through Contoso's scheme, so a LogoutRequest received through Mallory's scheme must not end her session, even when Mallory's IdP claims Contoso's entity id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_contosos_scheme_is_repointed_to_another_idp_expect_that_idps_logout_request_not_to_end_the_session()
    {
        using var fabrikamSigningKey = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=Fabrikam IdP");
        await using var fixture = await CreateFixtureAsync();
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);
        await RepointContosoSchemeAsync(fixture, FabrikamEntityId, fabrikamSigningKey);

        await SendLogoutRequestAsync(fixture, ContosoScheme, FabrikamEntityId, fabrikamSigningKey,
            aliceSignIn.Sub, sessionIndex: null);

        (await HasSessionAsync(fixture)).ShouldBeTrue("Contoso's IdP signed Alice in, so a LogoutRequest from Fabrikam's IdP must not end her session, even after Contoso's scheme was repointed to Fabrikam");
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task when_another_idps_logout_request_arrives_without_the_session_cookie_expect_the_browser_to_keep_the_session()
    {
        using var mallorySigningKey = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=Mallory IdP");
        await using var fixture = await CreateFixtureAsync();
        await AddMalloryIdpAsync(fixture, mallorySigningKey, malloryIdpEntityId: MalloryEntityId);
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        var responseToCrossSitePost = await SendLogoutRequestAsync(fixture, MalloryScheme, MalloryEntityId, mallorySigningKey,
            aliceSignIn.Sub, aliceSignIn.SessionIndex, withSessionCookie: false);
        ApplyResponseCookiesToBrowser(fixture, responseToCrossSitePost);

        (await HasSessionAsync(fixture)).ShouldBeTrue("a browser with a SameSite=Lax session cookie does not send it on another site's POST, but applies the response's cookies, so a LogoutRequest from Mallory's IdP must not delete Alice's session cookie");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_signing_idp_logs_out_another_user_expect_a_requester_logout_response_and_the_session_to_stay()
    {
        await using var fixture = await CreateFixtureAsync();
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        var responseToBobsLogoutRequest = await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey,
            "bob", aliceSignIn.SessionIndex);

        var logoutResponseToContoso = SamlTestHelpers.ExtractSamlLogoutResponseFromRedirect(responseToBobsLogoutRequest);
        logoutResponseToContoso.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Requester", "the request does not name Alice's session, so Contoso's IdP is told it was refused");
        (await HasSessionAsync(fixture)).ShouldBeTrue("Contoso's IdP signed a LogoutRequest for Bob, so Alice's session must stay");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_signing_idp_logs_out_another_session_index_expect_the_session_to_stay()
    {
        await using var fixture = await CreateFixtureAsync();
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey,
            aliceSignIn.Sub, "session-index-of-another-session");

        (await HasSessionAsync(fixture)).ShouldBeTrue("the LogoutRequest names a SessionIndex that is not Alice's, so Alice's session must stay");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_signing_idp_logs_out_the_session_user_in_another_name_id_format_expect_the_session_to_stay()
    {
        const string persistentNameIdFormat = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent";
        await using var fixture = await CreateFixtureAsync();
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey,
            aliceSignIn.Sub, aliceSignIn.SessionIndex, nameIdFormat: persistentNameIdFormat);

        (await HasSessionAsync(fixture)).ShouldBeTrue("Contoso gave Alice a NameID in another format, so a LogoutRequest for a persistent NameID with the same value names another user and must not end her session");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_session_is_stored_server_side_expect_a_logout_request_for_the_session_user_and_session_index_to_end_it()
    {
        await using var fixture = await CreateFixtureAsync(services => services.AddIdentityServerBuilder().AddServerSideSessions());
        var aliceSignIn = await SignInThroughContosoIdpAsync(fixture);

        await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey,
            aliceSignIn.Sub, aliceSignIn.SessionIndex);

        (await HasSessionAsync(fixture)).ShouldBeFalse("the server-side session keeps the SAML sign-in it was created from, so Contoso's LogoutRequest for Alice and her SessionIndex must end it");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_saml_sign_in_had_no_session_index_expect_a_logout_request_for_that_user_to_end_the_session()
    {
        await using var fixture = await CreateFixtureAsync();
        var contosoSamlResponse = await GetContosoSamlResponseAsync(fixture, signInAtIdp: true);
        var bobSamlResponse = contosoSamlResponse with { Xml = ResignAssertionForUserWithoutSessionIndex(contosoSamlResponse.Xml, "bob") };
        var bobCallbackResponse = await FollowRedirectsAsync(fixture, await PostToAcsAsync(fixture, bobSamlResponse));
        bobCallbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "precondition: Bob's callback completes");
        (await HasSessionAsync(fixture)).ShouldBeTrue("precondition: Bob signed in through Contoso's IdP");

        await SendLogoutRequestAsync(fixture, ContosoScheme, ContosoEntityId(fixture), ContosoSigningKey, "bob", sessionIndex: null);

        (await HasSessionAsync(fixture)).ShouldBeFalse("Bob's SAML sign-in had no SessionIndex, so his session carries no SAML sign-in to compare and the LogoutRequest ends it, as before");
    }

    private sealed record ExternalSignIn(string Sub, string SessionIndex);

    private sealed record CallbackBody(string? Sub, string? SessionIndex);

    private sealed record SamlResponse(string AcsUrl, string Xml, string? RelayState);

    private async Task<SamlResponse> GetContosoSamlResponseAsync(SamlDynamicProviderFixture fixture, bool signInAtIdp)
    {
        using var browserWithoutRedirects = BrowserWithoutRedirects(fixture);
        if (signInAtIdp)
        {
            await browserWithoutRedirects.GetAsync($"{fixture.IdpHost!.Uri()}/auto-login", _ct);
        }

        var idpInitiatedResponse = await browserWithoutRedirects.GetAsync(
            $"{fixture.IdpHost!.Uri()}/idp-initiated-sso?sp={Uri.EscapeDataString(fixture.SpHost!.Uri())}", _ct);
        idpInitiatedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (responseXml, relayState, acsUrl) = await SamlTestHelpers.ExtractSamlResponse(idpInitiatedResponse, _ct);
        return new SamlResponse(acsUrl, responseXml, relayState);
    }

    private async Task<HttpResponseMessage> PostToAcsAsync(SamlDynamicProviderFixture fixture, SamlResponse samlResponse)
    {
        var form = new Dictionary<string, string> { ["SAMLResponse"] = SamlTestHelpers.ConvertToBase64Encoded(samlResponse.Xml) };
        if (samlResponse.RelayState != null)
        {
            form["RelayState"] = samlResponse.RelayState;
        }

        using var browserWithoutRedirects = BrowserWithoutRedirects(fixture);
        return await browserWithoutRedirects.PostAsync(samlResponse.AcsUrl, new FormUrlEncodedContent(form), _ct);
    }

    private async Task<HttpResponseMessage> FollowRedirectsAsync(SamlDynamicProviderFixture fixture, HttpResponseMessage response)
    {
        using var browserWithoutRedirects = BrowserWithoutRedirects(fixture);
        while ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
        {
            response = await browserWithoutRedirects.GetAsync(new Uri(new Uri(fixture.SpHost!.Uri()), response.Headers.Location), _ct);
        }

        return response;
    }

    private static string ResignAssertionForUserWithoutSessionIndex(string responseXml, string nameId)
    {
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        document.LoadXml(responseXml);
        var assertion = (XmlElement)document.GetElementsByTagName("Assertion", SamlAssertionNamespace)[0]!;
        assertion.GetElementsByTagName("NameID", SamlAssertionNamespace)[0]!.InnerText = nameId;
        ((XmlElement)assertion.GetElementsByTagName("AuthnStatement", SamlAssertionNamespace)[0]!).RemoveAttribute("SessionIndex");
        var assertionSignature = assertion.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!;
        assertion.RemoveChild(assertionSignature);

        var signedAssertion = new SignedXml(assertion) { SigningKey = ContosoSigningKey.GetRSAPrivateKey() };
        signedAssertion.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedAssertion.SignedInfo.SignatureMethod = SigAlg;
        var reference = new Reference { Uri = "#" + assertion.GetAttribute("ID"), DigestMethod = SignedXml.XmlDsigSHA256Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedAssertion.AddReference(reference);
        signedAssertion.KeyInfo = new KeyInfo();
        signedAssertion.KeyInfo.AddClause(new KeyInfoX509Data(ContosoSigningKey));
        signedAssertion.ComputeSignature();

        var issuer = assertion.GetElementsByTagName("Issuer", SamlAssertionNamespace)[0];
        assertion.InsertAfter(document.ImportNode(signedAssertion.GetXml(), true), issuer);
        return document.OuterXml;
    }

    private async Task<SamlDynamicProviderFixture> CreateFixtureAsync(Action<IServiceCollection>? additionalSpServices = null)
    {
        var fixture = new SamlDynamicProviderFixture(output, additionalSpServices, configureSamlProvider: provider =>
        {
            provider.SingleLogoutServiceUrl = $"{provider.IdpEntityId}/SLO";
            provider.SpSigningCertificateBase64 = SamlFixture.StableSigningCert;
        });
        await fixture.InitializeAsync();
        return fixture;
    }

    private async Task AddMalloryIdpAsync(SamlDynamicProviderFixture fixture, X509Certificate2 mallorySigningKey, string malloryIdpEntityId)
    {
        await using var scope = fixture.SpHost!.ConfiguredServices.CreateAsyncScope();
        var configurationDb = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        configurationDb.IdentityProviders.Add(new SamlProvider
        {
            Scheme = MalloryScheme,
            Enabled = true,
            IdpEntityId = malloryIdpEntityId,
            SingleSignOnServiceUrl = "https://mallory.example/sso",
            SingleLogoutServiceUrl = "https://mallory.example/slo",
            SigningCertificateBase64 = Convert.ToBase64String(mallorySigningKey.Export(X509ContentType.Cert)),
            SpSigningCertificateBase64 = SamlFixture.StableSigningCert,
            BindingType = "redirect"
        }.ToEntity());
        await configurationDb.SaveChangesAsync(_ct);
    }

    private async Task RepointContosoSchemeAsync(SamlDynamicProviderFixture fixture, string idpEntityId, X509Certificate2 idpSigningKey)
    {
        var repointedProvider = await fixture.ReloadSamlProviderFromStoreAsync(ContosoScheme);
        repointedProvider.IdpEntityId = idpEntityId;
        repointedProvider.SigningCertificateBase64 = Convert.ToBase64String(idpSigningKey.Export(X509ContentType.Cert));

        await using var scope = fixture.SpHost!.ConfiguredServices.CreateAsyncScope();
        var configurationDb = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        configurationDb.IdentityProviders.Remove(configurationDb.IdentityProviders.Single(provider => provider.Scheme == ContosoScheme));
        await configurationDb.SaveChangesAsync(_ct);
        configurationDb.IdentityProviders.Add(repointedProvider.ToEntity());
        await configurationDb.SaveChangesAsync(_ct);
    }

    private async Task<ExternalSignIn> SignInThroughContosoIdpAsync(SamlDynamicProviderFixture fixture)
    {
        var idpUri = fixture.IdpHost!.Uri();
        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);

        var callbackResponse = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(fixture.SpHost!.Uri())}");
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var callback = await callbackResponse.Content.ReadFromJsonAsync<CallbackBody>(_ct);
        callback!.Sub.ShouldNotBeNullOrEmpty();
        callback.SessionIndex.ShouldNotBeNullOrEmpty();
        (await HasSessionAsync(fixture)).ShouldBeTrue("precondition: Alice signed in through Contoso's IdP");

        return new ExternalSignIn(callback.Sub!, callback.SessionIndex!);
    }

    private async Task<bool> HasSessionAsync(SamlDynamicProviderFixture fixture)
    {
        using var browserWithoutRedirects = BrowserWithoutRedirects(fixture);
        var sessionResponse = await browserWithoutRedirects.GetAsync($"{fixture.SpHost!.Uri()}/test/session", _ct);
        return sessionResponse.StatusCode == HttpStatusCode.OK;
    }

    private static HttpClient BrowserWithoutRedirects(SamlDynamicProviderFixture fixture) =>
        new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = fixture.BrowserCookies! });

    private async Task<HttpResponseMessage> SendLogoutRequestAsync(
        SamlDynamicProviderFixture fixture,
        string scheme,
        string issuer,
        X509Certificate2 signingKey,
        string nameId,
        string? sessionIndex,
        string? nameIdFormat = null,
        bool withSessionCookie = true)
    {
        var nameIdFormatAttribute = nameIdFormat == null ? "" : $" Format=\"{nameIdFormat}\"";
        var sessionIndexElement = sessionIndex == null ? "" : $"<samlp:SessionIndex>{sessionIndex}</samlp:SessionIndex>";
        var logoutRequestXml =
            $"""
             <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                  xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                  ID="_{Guid.NewGuid():N}" Version="2.0"
                                  IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
               <saml:Issuer>{issuer}</saml:Issuer>
               <saml:NameID{nameIdFormatAttribute}>{nameId}</saml:NameID>
               {sessionIndexElement}
             </samlp:LogoutRequest>
             """;

        var encodedLogoutRequest = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encodedLogoutRequest, null, signingKey, SigAlg);

        using var browserWithoutRedirects = withSessionCookie
            ? BrowserWithoutRedirects(fixture)
            : new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        return await browserWithoutRedirects.GetAsync(
            $"{fixture.SpHost!.Uri()}/federation/{scheme}/Saml2/Logout?SAMLRequest={encodedLogoutRequest}&SigAlg={Uri.EscapeDataString(sigAlg)}&Signature={Uri.EscapeDataString(signature)}",
            _ct);
    }

    private static void ApplyResponseCookiesToBrowser(SamlDynamicProviderFixture fixture, HttpResponseMessage response)
    {
        var spUri = new Uri(fixture.SpHost!.Uri());
        foreach (var setCookie in response.Headers.TryGetValues("Set-Cookie", out var setCookies) ? setCookies : [])
        {
            fixture.BrowserCookies!.SetCookies(spUri, setCookie);
        }
    }

    private static string ContosoEntityId(SamlDynamicProviderFixture fixture) => $"{fixture.IdpHost!.Uri()}/Saml2";

}

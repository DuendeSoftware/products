// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public sealed class SamlServiceProviderIdpInitiatedLogoutTests(ITestOutputHelper output)
{
    private const string Category = "Static SAML SP IdP-initiated logout";
    private const string SigAlg = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string StaticSpEntityId = "https://static-sp.example.com";
    private const string SamlAssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";

    private static readonly X509Certificate2 IdpSigningKey =
        X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(SamlFixture.StableSigningCert), null);

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly SamlServiceProvider _staticSpAtIdp = new()
    {
        EntityId = StaticSpEntityId,
        DisplayName = "Static SAML SP",
        Enabled = true,
        AllowIdpInitiated = true,
        AllowedScopes = new HashSet<string> { "openid", "profile" },
        AssertionConsumerServiceUrls = [],
        SigningBehavior = SamlSigningBehavior.SignAssertion,
        RequireSignedAuthnRequests = false
    };

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_idp_logs_out_the_session_user_with_the_session_index_expect_the_session_to_end()
    {
        await using var idpHost = await CreateIdpHostAsync();
        await using var staticSpHost = await CreateStaticSpHostAsync(idpHost.Uri());
        using var spBrowser = BrowserWithoutRedirects(new CookieContainer());
        var aliceSignIn = await SignInThroughIdpAsync(idpHost, staticSpHost, spBrowser);

        await SendLogoutRequestAsync(idpHost, staticSpHost, spBrowser, aliceSignIn.NameId, aliceSignIn.SessionIndex);

        (await HasSessionAsync(staticSpHost, spBrowser)).ShouldBeFalse("the IdP signed a LogoutRequest for Alice and her SessionIndex, so the session the static SP created for her must end");
    }

    private sealed record SamlSignIn(string NameId, string SessionIndex);

    private async Task<KestrelTestHost> CreateIdpHostAsync() =>
        await KestrelTestHost.Create(output,
            services =>
            {
                services.AddIdentityServer(options =>
                    {
                        options.UserInteraction.LoginUrl = "/auto-login";
                        options.KeyManagement.Enabled = false;
                    })
                    .AddSigningCredential(IdpSigningKey)
                    .AddInMemoryIdentityResources([new IdentityResources.OpenId(), new IdentityResources.Profile()])
                    .AddInMemorySamlServiceProviders([_staticSpAtIdp])
                    .AddSaml();
            },
            app =>
            {
                app.UseIdentityServer();
                app.MapGet("/auto-login", async (HttpContext ctx) =>
                {
                    var alice = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(JwtClaimTypes.Subject, "alice"),
                        new Claim(JwtClaimTypes.AuthenticationMethod, "pwd")
                    ], "pwd", JwtClaimTypes.Name, JwtClaimTypes.Role));
                    await ctx.SignInAsync(alice, new AuthenticationProperties());
                    return Results.Ok();
                });
                app.MapGet("/idp-initiated-sso", async (HttpContext ctx, IIdpInitiatedSsoService ssoService) =>
                {
                    var ssoResult = await ssoService.CreateResponseAsync(ctx, StaticSpEntityId, null, ctx.RequestAborted);
                    await ssoResult.Response!.ExecuteAsync(ctx);
                });
            },
            _ct);

    private async Task<KestrelTestHost> CreateStaticSpHostAsync(string idpUri)
    {
        var staticSpHost = await KestrelTestHost.Create(output,
            services =>
            {
                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddDeveloperSigningCredential(persistKey: false);
                services.AddAuthentication().AddSamlServiceProvider(options =>
                {
                    options.SpEntityId = StaticSpEntityId;
                    options.IdpEntityId = $"{idpUri}/Saml2";
                    options.SingleSignOnServiceUrl = $"{idpUri}/Saml2/SSO";
                    options.SingleLogoutServiceUrl = $"{idpUri}/Saml2/SLO";
                    options.SigningCertificatesBase64 = [Convert.ToBase64String(IdpSigningKey.Export(X509ContentType.Cert))];
                    options.SpSigningCertificateBase64 = SamlFixture.StableSigningCert;
                    options.AllowUnsolicitedAuthnResponse = true;
                    options.IdpInitiatedCallbackUrl = "/";
                    options.WantAssertionsSigned = false;
                });
            },
            app =>
            {
                app.UseIdentityServer();
                app.MapGet("/test/session", async (HttpContext ctx) =>
                    (await ctx.AuthenticateAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme)).Succeeded
                        ? Results.Ok()
                        : Results.Unauthorized());
            },
            _ct);

        _staticSpAtIdp.AssertionConsumerServiceUrls =
            [new IndexedEndpoint { Location = $"{staticSpHost.Uri()}/Saml2/Acs", Binding = SamlBinding.HttpPost, Index = 0, IsDefault = true }];
        return staticSpHost;
    }

    private async Task<SamlSignIn> SignInThroughIdpAsync(KestrelTestHost idpHost, KestrelTestHost staticSpHost, HttpClient spBrowser)
    {
        using var idpBrowser = BrowserWithoutRedirects(new CookieContainer());
        (await idpBrowser.GetAsync($"{idpHost.Uri()}/auto-login", _ct)).EnsureSuccessStatusCode();
        var idpInitiatedResponse = await idpBrowser.GetAsync($"{idpHost.Uri()}/idp-initiated-sso", _ct);
        idpInitiatedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "precondition: the IdP creates an IdP-initiated SAML Response");
        var (responseXml, relayState, acsUrl) = await SamlTestHelpers.ExtractSamlResponse(idpInitiatedResponse, _ct);

        var form = new Dictionary<string, string> { ["SAMLResponse"] = SamlTestHelpers.ConvertToBase64Encoded(responseXml) };
        if (relayState != null)
        {
            form["RelayState"] = relayState;
        }

        var acsResponse = await spBrowser.PostAsync(acsUrl, new FormUrlEncodedContent(form), _ct);
        acsResponse.StatusCode.ShouldBe(HttpStatusCode.SeeOther, "precondition: the static SP accepts the IdP-initiated SAML Response");
        (await HasSessionAsync(staticSpHost, spBrowser)).ShouldBeTrue("precondition: the static SP signed Alice into the session");

        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        document.LoadXml(responseXml);
        var nameId = document.GetElementsByTagName("NameID", SamlAssertionNamespace)[0]!.InnerText;
        var sessionIndex = ((XmlElement)document.GetElementsByTagName("AuthnStatement", SamlAssertionNamespace)[0]!).GetAttribute("SessionIndex");
        return new SamlSignIn(nameId, sessionIndex);
    }

    private async Task SendLogoutRequestAsync(KestrelTestHost idpHost, KestrelTestHost staticSpHost, HttpClient spBrowser, string nameId, string sessionIndex)
    {
        var logoutRequestXml =
            $"""
             <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                  xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                  ID="_{Guid.NewGuid():N}" Version="2.0"
                                  IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
               <saml:Issuer>{idpHost.Uri()}/Saml2</saml:Issuer>
               <saml:NameID>{nameId}</saml:NameID>
               <samlp:SessionIndex>{sessionIndex}</samlp:SessionIndex>
             </samlp:LogoutRequest>
             """;

        var encodedLogoutRequest = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encodedLogoutRequest, null, IdpSigningKey, SigAlg);

        await spBrowser.GetAsync(
            $"{staticSpHost.Uri()}/Saml2/Logout?SAMLRequest={encodedLogoutRequest}&SigAlg={Uri.EscapeDataString(sigAlg)}&Signature={Uri.EscapeDataString(signature)}",
            _ct);
    }

    private async Task<bool> HasSessionAsync(KestrelTestHost staticSpHost, HttpClient spBrowser)
    {
        var sessionResponse = await spBrowser.GetAsync($"{staticSpHost.Uri()}/test/session", _ct);
        return sessionResponse.StatusCode == HttpStatusCode.OK;
    }

    private static HttpClient BrowserWithoutRedirects(CookieContainer cookies) =>
        new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies });
}

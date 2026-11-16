// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

/// <summary>
/// Verifies that the SAML SP endpoints reject inbound text elements that contain child elements
/// with an <see cref="System.Xml.XmlException"/>. Each nested value flattens to a valid
/// string, so a message that is not rejected as malformed fails later with a different exception.
/// </summary>
public class SamlDynamicProviderTextContentTests(ITestOutputHelper output)
{
    private const string Category = "Dynamic SAML provider text content";
    private const string AcsPath = "/federation/saml-idp/Saml2/Acs";
    private const string LogoutPath = "/federation/saml-idp/Saml2/Logout";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_post_has_nested_status_message()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var response = await PostAsync(fixture, AcsPath, "SAMLResponse", ResponseXml(statusMessageContent: Nest("message")));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_redirect_logout_has_nested_issuer()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);
        var issuer = IdpEntityId(fixture);

        var encoded = await SamlTestHelpers.EncodeRequest(LogoutRequestXml(Nest(issuer), "user", "session"), _ct);
        var invalidSignature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256));

        var response = await SendRedirectAsync(fixture,
            $"SAMLRequest={encoded}&SigAlg={Uri.EscapeDataString(SigAlg)}&Signature={Uri.EscapeDataString(invalidSignature)}");

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_unsigned_post_logout_has_nested_issuer()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);
        var issuer = IdpEntityId(fixture);

        var response = await PostAsync(fixture, LogoutPath, "SAMLRequest", LogoutRequestXml(Nest(issuer), "user", "session"));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signed_redirect_logout_has_nested_name_id()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var response = await SendSignedRedirectAsync(fixture, LogoutRequestXml(IdpEntityId(fixture), Nest("user"), "session"));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_signed_redirect_logout_has_nested_session_index()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var response = await SendSignedRedirectAsync(fixture, LogoutRequestXml(IdpEntityId(fixture), "user", Nest("session")));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    private const string SigAlg = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

    private async Task<SamlDynamicProviderFixture> CreateFixtureAsync(ExceptionRecorder recorder)
    {
        var fixture = new SamlDynamicProviderFixture(output,
            additionalSpServices: services => services.AddSingleton<IStartupFilter>(recorder));
        await fixture.InitializeAsync();
        return fixture;
    }

    private static string IdpEntityId(SamlDynamicProviderFixture fixture) => $"{fixture.IdpHost!.Uri()}/Saml2";

    private static string Nest(string value) => $"<x>{value}</x>";

    private static string ResponseXml(string statusMessageContent) =>
        $"""
         <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                         xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                         ID="_{Guid.NewGuid():N}" Version="2.0"
                         IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
           <saml:Issuer>https://idp.example.com</saml:Issuer>
           <samlp:Status>
             <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
             <samlp:StatusMessage>{statusMessageContent}</samlp:StatusMessage>
           </samlp:Status>
         </samlp:Response>
         """;

    private static string LogoutRequestXml(string issuerContent, string nameIdContent, string sessionIndexContent) =>
        $"""
         <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                              xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                              ID="_{Guid.NewGuid():N}" Version="2.0"
                              IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
           <saml:Issuer>{issuerContent}</saml:Issuer>
           <saml:NameID>{nameIdContent}</saml:NameID>
           <samlp:SessionIndex>{sessionIndexContent}</samlp:SessionIndex>
         </samlp:LogoutRequest>
         """;

    private async Task<HttpResponseMessage> SendSignedRedirectAsync(SamlDynamicProviderFixture fixture, string logoutRequestXml)
    {
        var encoded = await SamlTestHelpers.EncodeRequest(logoutRequestXml, _ct);
        using var idpSigningCert = X509CertificateLoader.LoadPkcs12(
            Convert.FromBase64String(SamlFixture.StableSigningCert), null);
        var (signature, sigAlg) = SamlTestHelpers.SignAuthNRequestRedirect(encoded, null, idpSigningCert, SigAlg);

        return await SendRedirectAsync(fixture,
            $"SAMLRequest={encoded}&SigAlg={Uri.EscapeDataString(sigAlg)}&Signature={Uri.EscapeDataString(signature)}");
    }

    private async Task<HttpResponseMessage> SendRedirectAsync(SamlDynamicProviderFixture fixture, string query)
    {
        using var client = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        return await client.GetAsync($"{LogoutPath}?{query}", _ct);
    }

    private async Task<HttpResponseMessage> PostAsync(SamlDynamicProviderFixture fixture, string path, string parameter, string xml)
    {
        using var client = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [parameter] = SamlTestHelpers.ConvertToBase64Encoded(xml)
        });
        return await client.PostAsync(path, content, _ct);
    }
}

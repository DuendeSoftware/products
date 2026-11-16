// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Xml;
using Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Internal.Saml.Sp.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSpXmlParsingTests(ITestOutputHelper output)
{
    private const string Category = "SAML SP XML parsing";
    private const string AcsPath = "/federation/saml-idp/Saml2/Acs";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_post_contains_dtd()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var response = await PostResponseAsync(fixture, "<!DOCTYPE samlp:Response []>" + ResponseXml());

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_post_contains_empty_payload()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var response = await PostResponseAsync(fixture, "");

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_post_has_unsupported_version()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        await PostResponseAsync(fixture, ResponseXml(version: "1.0"));

        var exception = recorder.Exception.ShouldBeOfType<BadFormatSamlResponseException>();
        exception.InnerException.ShouldBeOfType<XmlException>().Message.ShouldBe("Wrong or unsupported SAML2 version");
        exception.Data["Saml2Response"].ShouldNotBeNull();
    }

    private async Task<SamlDynamicProviderFixture> CreateFixtureAsync(ExceptionRecorder recorder)
    {
        var fixture = new SamlDynamicProviderFixture(output,
            additionalSpServices: services => services.AddSingleton<IStartupFilter>(recorder));
        await fixture.InitializeAsync();
        return fixture;
    }

    private async Task<HttpResponseMessage> PostResponseAsync(SamlDynamicProviderFixture fixture, string xml)
    {
        using var client = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SAMLResponse"] = SamlTestHelpers.ConvertToBase64Encoded(xml)
        });
        return await client.PostAsync(AcsPath, content, _ct);
    }

    private static string ResponseXml(string version = "2.0") =>
        $"""
         <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                         xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                         ID="_{Guid.NewGuid():N}" Version="{version}"
                         IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
           <saml:Issuer>https://idp.example.com</saml:Issuer>
           <samlp:Status>
             <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
           </samlp:Status>
         </samlp:Response>
         """;
}

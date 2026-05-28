// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Serialization;
using UnitTests.Common;
using ISaml2IssuerNameService = Duende.IdentityServer.Saml.Services.ISaml2IssuerNameService;
using NameId = Duende.IdentityServer.Saml.NameId;
using SamlLogoutRequest = Duende.IdentityServer.Saml.Samlp.LogoutRequest;
using SamlStatusCodes = Duende.IdentityServer.Saml.Models.SamlStatusCodes;
using SamlVersions = Duende.IdentityServer.Saml.Models.SamlVersions;
using ValidatedLogoutRequest = Duende.IdentityServer.Saml.Validation.ValidatedLogoutRequest;

namespace UnitTests.Saml;

public sealed class Saml2SloResponseGeneratorTests
{
    private const string Category = "Saml2SloResponseGenerator";
    private const string IdpEntityId = "https://idp.example.com";
    private const string SpEntityId = "https://sp.example.com";
    private const string SpSloUrl = "https://sp.example.com/slo";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static Saml2SloResponseGenerator CreateGenerator()
        => new(
            new StubIssuerNameService(IdpEntityId),
            TimeProvider.System,
            new SamlXmlWriter(),
            new MockSamlSigningService(TestCert.Load()));

    private static ValidatedLogoutRequest CreateRequest(string? relayState = null)
    {
        var xmlDoc = new XmlDocument();
        var xmlElement = xmlDoc.CreateElement("SAMLRequest");

        return new ValidatedLogoutRequest
        {
            LogoutRequest = new SamlLogoutRequest
            {
                Id = "_request-id-123",
                Version = SamlVersions.V2,
                IssueInstant = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                Issuer = new NameId(SpEntityId),
                NameId = new NameId("user@example.com")
            },
            Binding = SamlConstants.Bindings.HttpRedirect,
            Saml2Message = new InboundSaml2Message
            {
                Name = "SAMLRequest",
                Xml = xmlElement,
                Destination = IdpEntityId + "/Saml2/SLO",
                Binding = SamlConstants.Bindings.HttpRedirect,
                RelayState = relayState
            },
            Saml2Sp = new SamlServiceProvider
            {
                EntityId = SpEntityId,
                SingleLogoutServiceUrls = [new SamlEndpointType { Location = SpSloUrl, Binding = SamlBinding.HttpRedirect }]
            },
            Saml2IdpEntityId = IdpEntityId
        };
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SuccessResponseHasCorrectStatusCode()
    {
        var generator = CreateGenerator();
        var request = CreateRequest();

        var result = await generator.CreateSuccessResponse(request, _ct);

        result.Error.ShouldBeNull();
        result.Message.ShouldNotBeNull();

        var nsMgr = new XmlNamespaceManager(result.Message.Xml.OwnerDocument.NameTable);
        nsMgr.AddNamespace("samlp", SamlConstants.Namespaces.Protocol);
        var statusCode = result.Message.Xml.SelectSingleNode("samlp:Status/samlp:StatusCode/@Value", nsMgr)?.Value;
        statusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SuccessResponseHasInResponseTo()
    {
        var generator = CreateGenerator();
        var request = CreateRequest();

        var result = await generator.CreateSuccessResponse(request, _ct);

        result.Message.ShouldNotBeNull();
        var inResponseTo = result.Message.Xml.GetAttribute("InResponseTo");
        inResponseTo.ShouldBe("_request-id-123");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SuccessResponseHasIssuer()
    {
        var generator = CreateGenerator();
        var request = CreateRequest();

        var result = await generator.CreateSuccessResponse(request, _ct);

        result.Message.ShouldNotBeNull();
        var nsMgr = new XmlNamespaceManager(result.Message.Xml.OwnerDocument.NameTable);
        nsMgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        var issuer = result.Message.Xml.SelectSingleNode("saml:Issuer", nsMgr)?.InnerText;
        issuer.ShouldBe(IdpEntityId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SuccessResponseDestinationIsSpSloUrl()
    {
        var generator = CreateGenerator();
        var request = CreateRequest();

        var result = await generator.CreateSuccessResponse(request, _ct);

        result.Message.ShouldNotBeNull();
        result.Message.Destination.ShouldBe(SpSloUrl);
        result.Message.Xml.GetAttribute("Destination").ShouldBe(SpSloUrl);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SuccessResponsePreservesRelayState()
    {
        var generator = CreateGenerator();
        var request = CreateRequest(relayState: "my-relay-state");

        var result = await generator.CreateSuccessResponse(request, _ct);

        result.Message.ShouldNotBeNull();
        result.Message.RelayState.ShouldBe("my-relay-state");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ResponseIdIsValidNcName()
    {
        var generator = CreateGenerator();
        var request = CreateRequest();

        var result = await generator.CreateSuccessResponse(request, _ct);

        result.Message.ShouldNotBeNull();
        var id = result.Message.Xml.GetAttribute("ID");
        id.ShouldNotBeNullOrEmpty();
        char.IsDigit(id![0]).ShouldBeFalse("XML ID must not start with a digit (xs:ID / NCName requirement)");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ResponseBindingMatchesRequestBinding()
    {
        var generator = CreateGenerator();
        var request = CreateRequest();

        var result = await generator.CreateSuccessResponse(request, _ct);

        result.Message.ShouldNotBeNull();
        result.Message.Binding.ShouldBe(SamlConstants.Bindings.HttpRedirect);
    }

    private sealed class StubIssuerNameService(string entityId) : ISaml2IssuerNameService
    {
        public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(entityId);
    }
}

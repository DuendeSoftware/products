// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Common;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;
using UnitTests.Saml.TestData;

namespace UnitTests.Saml;

public sealed class SamlXmlReaderXmlFileTests
{
    private const string Category = "SamlXmlReader XmlFile";

    private readonly SamlXmlReader _reader = new();

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAuthnRequest_Mandatory()
    {
        var traverser = XmlTestData.GetXmlTraverser<SamlXmlReaderXmlFileTests>();

        var result = await _reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.Id.ShouldBe("x123");
        result.Version.ShouldBe("2.0");
        result.IssueInstant.ShouldBe(new DateTimeUtc(2023, 11, 24, 22, 44, 14));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAuthnRequest_CanReadOptional()
    {
        var traverser = XmlTestData.GetXmlTraverser<SamlXmlReaderXmlFileTests>();

        var result = await _reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None);

        result.Id.ShouldBe("x123");
        result.IssueInstant.ShouldBe(new DateTimeUtc(2023, 11, 24, 22, 44, 14));
        result.Destination.ShouldBe("https://idp.example.com/Sso");
        result.Consent.ShouldBe("urn:oasis:names:tc:SAML:2.0:consent:obtained");
        result.Issuer!.Value.ShouldBe("https://sp.example.com/Metadata");
        result.ForceAuthn.ShouldBeTrue();
        result.IsPassive.ShouldBeTrue();
        result.AssertionConsumerServiceUrl.ShouldBe("https://sp.example.com/Acs");
        result.AssertionConsumerServiceIndex.ShouldBe(42);
        result.ProtocolBinding.ShouldBe("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");
        result.AttributeConsumingServiceIndex.ShouldBe(17);
        result.ProviderName.ShouldBe("test");

        // NameIdPolicy
        result.NameIdPolicy.ShouldNotBeNull();
        result.NameIdPolicy!.Format.ShouldBe("urn:oasis:names:tc:SAML:2.0:nameid-format:encrypted");
        result.NameIdPolicy.AllowCreate.ShouldBe(true);
        result.NameIdPolicy.SPNameQualifier.ShouldBe("urn:oasis:names:tc:SAML:1.1:nameid-format:persistent");

        // Conditions
        result.Conditions.ShouldNotBeNull();
        result.Conditions!.NotBefore.ShouldBe(new DateTimeUtc(2023, 11, 24, 22, 44, 14));

        // RequestedAuthnContext
        result.RequestedAuthnContext.ShouldNotBeNull();
        result.RequestedAuthnContext!.Comparison.ShouldBe("better");
        result.RequestedAuthnContext.AuthnContextClassRef.Count.ShouldBe(2);
        result.RequestedAuthnContext.AuthnContextClassRef[0].ShouldBe("urn:ContextClassRef");
        result.RequestedAuthnContext.AuthnContextClassRef[1].ShouldBe("urn:AnotherContextClassRef");

        // Scoping
        result.Scoping.ShouldNotBeNull();
        result.Scoping!.ProxyCount.ShouldBe(1);
        result.Scoping.IDPList.ShouldNotBeNull();
        result.Scoping.IDPList!.IdpEntries.Count.ShouldBe(2);
        result.Scoping.IDPList.IdpEntries[0].ProviderId.ShouldBe("https://stubidp.sustainsys.com/Metadata");
        result.Scoping.IDPList.IdpEntries[0].Name.ShouldBe("Sustainsys Stub Idp");
        result.Scoping.IDPList.IdpEntries[0].Loc.ShouldBe("https://stubidp.sustainsys.com");
        result.Scoping.IDPList.IdpEntries[1].ProviderId.ShouldBe("https://idp.example.com/Metadata");
        result.Scoping.IDPList.GetComplete.ShouldBe("https://example.com/GetComplete");
        result.Scoping.RequesterID.Count.ShouldBe(2);
        result.Scoping.RequesterID[0].ShouldBe("https://example.com/RequesterID?query=123");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAuthnRequest_ErrorCallback()
    {
        var traverser = XmlTestData.GetXmlTraverser<SamlXmlReaderXmlFileTests>(testName: "ReadAuthnRequest_Error");

        ReadErrorInspectorContext<AuthnRequest>? capturedContext = null;
        Action<ReadErrorInspectorContext<AuthnRequest>> errorInspector = context =>
        {
            capturedContext = context;
            foreach (var error in context.Errors)
            {
                error.Ignore = true;
            }
        };

        await _reader.ReadAuthnRequestAsync(traverser, errorInspector, CancellationToken.None);

        capturedContext.ShouldNotBeNull();
        capturedContext!.Errors.Count.ShouldBeGreaterThan(0);
        capturedContext.Errors.ShouldContain(e => e.LocalName == "Version");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReadAuthnRequest_Error()
    {
        var traverser = XmlTestData.GetXmlTraverser<SamlXmlReaderXmlFileTests>();

        var ex = await Should.ThrowAsync<SamlXmlException>(
            _reader.ReadAuthnRequestAsync(traverser, null, CancellationToken.None));

        ex.Message.ShouldContain("Version");
    }
}

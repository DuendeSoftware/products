// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;

namespace UnitTests.Saml;

public sealed class SamlXmlReaderErrorInspectorTests
{
    private const string Category = "SamlXmlReader Error Inspector";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    // Minimal valid Response XML — removing IssueInstant triggers a MissingAttribute error
    private const string MinimalResponseXml = """
        <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                        xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                        ID="_response-1"
                        Version="2.0"
                        IssueInstant="2025-01-01T00:00:00Z">
            <saml:Issuer>https://idp.example.com</saml:Issuer>
            <samlp:Status>
                <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success"/>
            </samlp:Status>
        </samlp:Response>
        """;

    // Minimal valid EntityDescriptor XML — removing entityID triggers a MissingAttribute error
    private const string MinimalEntityDescriptorXml = """
        <md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata"
                             entityID="https://sp.example.com">
            <md:SPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
                <md:AssertionConsumerService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
                                            Location="https://sp.example.com/acs"
                                            index="0"/>
            </md:SPSSODescriptor>
        </md:EntityDescriptor>
        """;

    [Fact]
    [Trait("Category", Category)]
    public async Task ResponseErrorInspectorIsCalledOnError()
    {
        var doc = new XmlDocument();
        doc.LoadXml(MinimalResponseXml);
        doc.DocumentElement!.RemoveAttribute("IssueInstant");

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var reader = new SamlXmlReader();
        var inspectorCalled = false;

        void errorInspector(ReadErrorInspectorContext<Response> context)
        {
            inspectorCalled = true;

            context.Data.ShouldNotBeNull();
            context.Data.Id.ShouldBe("_response-1");
            context.XmlSource.ShouldBe(doc.DocumentElement);
            context.Errors.Count.ShouldBe(1);

            var error = context.Errors.Single();
            error.Reason.ShouldBe(ErrorReason.MissingAttribute);
            error.LocalName.ShouldBe("IssueInstant");
            error.Ignore.ShouldBeFalse();

            error.Ignore = true;
        }

        var result = await reader.ReadResponseAsync(traverser, errorInspector, _ct);

        inspectorCalled.ShouldBeTrue();
        result.Id.ShouldBe("_response-1");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ResponseErrorInspectorIsNotCalledWhenNoErrors()
    {
        var doc = new XmlDocument();
        doc.LoadXml(MinimalResponseXml);

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var reader = new SamlXmlReader();
        var inspectorCalled = false;

        void errorInspector(ReadErrorInspectorContext<Response> context)
        {
            inspectorCalled = true;
        }

        await reader.ReadResponseAsync(traverser, errorInspector, _ct);

        inspectorCalled.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EntityDescriptorErrorInspectorIsCalledOnError()
    {
        var doc = new XmlDocument();
        doc.LoadXml(MinimalEntityDescriptorXml);
        doc.DocumentElement!.RemoveAttribute("entityID");

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var reader = new SamlXmlReader();
        var inspectorCalled = false;

        void errorInspector(ReadErrorInspectorContext<EntityDescriptor> context)
        {
            inspectorCalled = true;

            context.Data.ShouldNotBeNull();
            context.XmlSource.ShouldBe(doc.DocumentElement);
            context.Errors.Count.ShouldBeGreaterThan(0);

            var error = context.Errors.First(e => e.Reason == ErrorReason.MissingAttribute);
            error.LocalName.ShouldBe("entityID");
            error.Ignore.ShouldBeFalse();

            // Mark all errors as ignored to prevent ThrowOnErrors
            foreach (var e in context.Errors)
            {
                e.Ignore = true;
            }
        }

        var result = await reader.ReadEntityDescriptorAsync(traverser, errorInspector, _ct);

        inspectorCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EntityDescriptorErrorInspectorIsNotCalledWhenNoErrors()
    {
        var doc = new XmlDocument();
        doc.LoadXml(MinimalEntityDescriptorXml);

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var reader = new SamlXmlReader();
        var inspectorCalled = false;

        void errorInspector(ReadErrorInspectorContext<EntityDescriptor> context)
        {
            inspectorCalled = true;
        }

        await reader.ReadEntityDescriptorAsync(traverser, errorInspector, _ct);

        inspectorCalled.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ScopingWithUnexpectedElementReportsExtraElementsError()
    {
        // AuthnRequest with a Scoping containing an unexpected element
        const string xml = """
            <samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                ID="_req-1"
                                Version="2.0"
                                IssueInstant="2025-01-01T00:00:00Z">
                <saml:Issuer>https://sp.example.com</saml:Issuer>
                <samlp:Scoping>
                    <samlp:Bogus>unexpected</samlp:Bogus>
                </samlp:Scoping>
            </samlp:AuthnRequest>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var traverser = new XmlTraverser(doc.DocumentElement!);
        var reader = new SamlXmlReader();
        var inspectorCalled = false;

        void errorInspector(ReadErrorInspectorContext<AuthnRequest> context)
        {
            inspectorCalled = true;

            var error = context.Errors.First(e => e.Reason == ErrorReason.ExtraElements);
            error.LocalName.ShouldBe("Bogus");

            foreach (var e in context.Errors)
            {
                e.Ignore = true;
            }
        }

        var result = await reader.ReadAuthnRequestAsync(traverser, errorInspector, _ct);

        inspectorCalled.ShouldBeTrue();
        result.Scoping.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void GetTextContentsReportsErrorForNonTextContent()
    {
        // An element that should contain only text but has a child element
        const string xml = """
            <saml:Issuer xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
                <saml:Nested>unexpected</saml:Nested>
            </saml:Issuer>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var rootTraverser = new XmlTraverser(doc.DocumentElement!);
        var text = rootTraverser.GetTextContents();

        text.ShouldBe("unexpected");
        rootTraverser.Errors.Count.ShouldBe(1);

        var error = rootTraverser.Errors.Single();
        error.Reason.ShouldBe(ErrorReason.ExtraElements);
        error.LocalName.ShouldBe("Nested");
    }

    [Fact]
    [Trait("Category", Category)]
    public void GetTextContentsDoesNotReportErrorForPureText()
    {
        const string xml = """
            <saml:Issuer xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">https://idp.example.com</saml:Issuer>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var rootTraverser = new XmlTraverser(doc.DocumentElement!);
        var text = rootTraverser.GetTextContents();

        text.ShouldBe("https://idp.example.com");
        rootTraverser.Errors.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void GetTextContentsReportsErrorForCData()
    {
        const string xml = """
            <saml:Issuer xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"><![CDATA[unexpected]]></saml:Issuer>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var rootTraverser = new XmlTraverser(doc.DocumentElement!);
        var text = rootTraverser.GetTextContents();

        text.ShouldBe("unexpected");
        rootTraverser.Errors.Count.ShouldBe(1);
        var error = rootTraverser.Errors.Single();
        error.Reason.ShouldBe(ErrorReason.ExtraElements);
    }

    [Fact]
    [Trait("Category", Category)]
    public void GetTextContentsAllowsComments()
    {
        const string xml = """
            <saml:Issuer xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"><!-- a comment -->https://idp.example.com</saml:Issuer>
            """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var rootTraverser = new XmlTraverser(doc.DocumentElement!);
        var text = rootTraverser.GetTextContents();

        text.ShouldBe("https://idp.example.com");
        rootTraverser.Errors.Count.ShouldBe(0);
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace UnitTests.Saml.Sp;

public sealed class Saml2LogoutRequestTests
{
    [Fact]
    public void error_when_logout_request_has_nested_issuer_in_base_properties()
    {
        var document = new XmlDocument();
        document.LoadXml("""
            <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                 xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                 ID="_logout" Version="2.0" IssueInstant="2026-01-01T00:00:00Z">
              <saml:Issuer><x>https://idp.example.com</x></saml:Issuer>
              <saml:NameID>user</saml:NameID>
            </samlp:LogoutRequest>
            """);

        var exception = Should.Throw<XmlException>(() => Saml2LogoutRequest.FromXml(document.DocumentElement));

        exception.Message.ShouldBe("Element \"Issuer\" must contain text only.");
    }

    [Fact]
    public void success_when_logout_request_contains_split_and_escaped_text()
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml("""
            <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                 xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                 ID="_logout" Version="2.0" IssueInstant="2026-01-01T00:00:00Z">
              <saml:Issuer>https://idp.<!-- comment -->example.com?a=1&amp;b=2</saml:Issuer>
              <saml:NameID>u<![CDATA[se]]><!-- comment -->r</saml:NameID>
              <samlp:SessionIndex>sess<!-- comment -->ion</samlp:SessionIndex>
            </samlp:LogoutRequest>
            """);

        var request = Saml2LogoutRequest.FromXml(document.DocumentElement);

        request.Issuer.Id.ShouldBe("https://idp.example.com?a=1&b=2");
        request.NameId.Value.ShouldBe("user");
        request.SessionIndex.ShouldBe("session");
    }
}

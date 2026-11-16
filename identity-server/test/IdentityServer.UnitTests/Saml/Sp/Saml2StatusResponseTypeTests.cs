// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace UnitTests.Saml.Sp;

public sealed class Saml2StatusResponseTypeTests
{
    [Fact]
    public void error_when_logout_response_has_nested_issuer_in_base_properties()
    {
        var document = new XmlDocument();
        document.LoadXml("""
            <samlp:LogoutResponse xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                  xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                  ID="_logout-response" Version="2.0" IssueInstant="2026-01-01T00:00:00Z">
              <saml:Issuer><x>https://idp.example.com</x></saml:Issuer>
              <samlp:Status>
                <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
              </samlp:Status>
            </samlp:LogoutResponse>
            """);

        var exception = Should.Throw<XmlException>(() => Saml2LogoutResponse.FromXml(document.DocumentElement));

        exception.Message.ShouldBe("Element \"Issuer\" must contain text only.");
    }
}

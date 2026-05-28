// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Common;
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using UnitTests.Saml.TestData;
using SamlAttribute = Duende.IdentityServer.Saml.SamlAttribute;

namespace UnitTests.Saml;

public sealed class SamlXmlWriterXmlFileTests
{
    private const string Category = "SamlXmlWriter XmlFile";

    private readonly SamlXmlWriter _writer = new();

    [Fact]
    [Trait("Category", Category)]
    public void WriteEntityDescriptor()
    {
        var idpDescriptor = new IDPSSODescriptor();
        idpDescriptor.SingleLogoutServices.Add(new Endpoint
        {
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect",
            Location = "https://stubidp.sustainsys.com/Logout"
        });
        idpDescriptor.SingleLogoutServices.Add(new Endpoint
        {
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST",
            Location = "https://stubidp.sustainsys.com/Logout"
        });
        idpDescriptor.SingleSignOnServices.Add(new Endpoint
        {
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect",
            Location = "https://stubidp.sustainsys.com/"
        });
        idpDescriptor.SingleSignOnServices.Add(new Endpoint
        {
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST",
            Location = "https://stubidp.sustainsys.com/"
        });

        var entityDescriptor = new EntityDescriptor
        {
            Id = "_ae48f365-7f3f-4b82-a17f-08eb31347e65",
            EntityId = "https://stubidp.sustainsys.com/Metadata",
            CacheDuration = TimeSpan.FromMinutes(15),
            ValidUntil = new DateTimeUtc(2025, 12, 16, 12, 7, 49)
        };
        entityDescriptor.RoleDescriptors.Add(idpDescriptor);

        var actual = _writer.Write(entityDescriptor);
        var expected = XmlTestData.GetXmlDocument<SamlXmlWriterXmlFileTests>();

        expected.ShouldNotBeNull();
        actual.OuterXml.ShouldBe(expected!.OuterXml);
    }

    [Fact]
    [Trait("Category", Category)]
    public void WriteSamlResponse_MinimalErrorRequester()
    {
        var response = new Response
        {
            Id = "x123",
            Version = "2.0",
            IssueInstant = new DateTimeUtc(2025, 10, 7, 11, 13, 32),
            Status = new SamlStatus
            {
                StatusCode = new StatusCode { Value = SamlStatusCodes.Requester }
            }
        };

        var actual = _writer.Write(response);
        var expected = XmlTestData.GetXmlDocument<SamlXmlWriterXmlFileTests>();

        expected.ShouldNotBeNull();
        actual.OuterXml.ShouldBe(expected!.OuterXml);
    }

    [Fact]
    [Trait("Category", Category)]
    public void WriteSamlResponse_CompleteSuccess()
    {
        var response = new Response
        {
            Id = "x123",
            InResponseTo = "x789",
            Version = "2.0",
            IssueInstant = new DateTimeUtc(2025, 10, 7, 13, 46, 32),
            Destination = "https://sp.example.com/Saml2/Acs",
            Issuer = new NameId("https://idp.example.com/Metadata", "urn:oasis:names:tc:SAML:1.1:nameid-format:entity"),
            Status = new SamlStatus
            {
                StatusCode = new StatusCode { Value = SamlStatusCodes.Success }
            }
        };

        response.Assertions.Add(new Assertion
        {
            Version = "2.42",
            Id = "_0f9174fb-a286-43cf-93c8-197dfc6c58d2",
            IssueInstant = new DateTimeUtc(2025, 10, 7, 13, 46, 33),
            Issuer = new NameId("https://idp.example.com/Metadata"),
            Subject = new Subject
            {
                NameId = new NameId("x123456", "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"),
                SubjectConfirmation = new SubjectConfirmation
                {
                    Method = "urn:oasis:names:tc:SAML:2.0:cm:bearer",
                    SubjectConfirmationData = new SubjectConfirmationData
                    {
                        NotOnOrAfter = new DateTimeUtc(2024, 2, 12, 13, 2, 53),
                        Recipient = "https://sp.example.com/Saml2/Acs",
                        InResponseTo = "x789"
                    }
                }
            },
            Conditions = new Conditions
            {
                NotOnOrAfter = new DateTimeUtc(2025, 10, 7, 14, 46, 32)
            },
            AuthnStatement = new AuthnStatement
            {
                AuthnInstant = new DateTimeUtc(2024, 2, 12, 13, 0, 53),
                SessionIndex = "42",
                AuthnContext = new AuthnContext
                {
                    AuthnContextClassRef = "urn:oasis:names:tc:SAML:2.0:ac:classes:unspecified"
                }
            },
            Attributes =
            [
                new SamlAttribute { Name = "organisation", Values = { "Sustainsys AB" } },
                new SamlAttribute { Name = "email", Values = { "primary@example.com", "secondary@example.com" } },
                new SamlAttribute { Name = "NullAttribute", Values = { null } },
                new SamlAttribute { Name = "EmptyAttribute", Values = { "" } }
            ]
        });

        // Add audience restriction
        response.Assertions[0].Conditions!.AudienceRestrictions.Add(new AudienceRestriction
        {
            Audiences = { "https://sp.example.com/Saml2" }
        });

        var actual = _writer.Write(response);
        var expected = XmlTestData.GetXmlDocument<SamlXmlWriterXmlFileTests>();

        expected.ShouldNotBeNull();
        actual.OuterXml.ShouldBe(expected!.OuterXml);
    }
}

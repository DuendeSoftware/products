// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Xml;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

internal class SamlDataBuilder(SamlData data)
{
    public SamlServiceProvider SamlServiceProvider(
        System.Security.Cryptography.X509Certificates.X509Certificate2? signingCertificate = null,
        bool requireSignedAuthnRequests = false,
        System.Security.Cryptography.X509Certificates.X509Certificate2[]? encryptionCertificates = null,
        bool encryptAssertions = false,
        string? entityId = null,
        SamlBinding acsBinding = SamlBinding.HttpPost) => new SamlServiceProvider
        {
            EntityId = entityId ?? data.EntityId,
            DisplayName = "Example SP",
            Description = "Example SP",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid", "profile", "email", "custom" },
            RequireSignedAuthnRequests = requireSignedAuthnRequests || signingCertificate != null,
            Certificates = BuildCertificates(signingCertificate, encryptionCertificates),
            AssertionConsumerServiceUrls = [new IndexedEndpoint { Location = data.AcsUrl.OriginalString, Binding = acsBinding }],
            SingleLogoutServiceUrls = [new SamlEndpointType { Location = data.SingleLogoutServiceUrl.OriginalString, Binding = SamlBinding.HttpRedirect }],
            RequestMaxAge = TimeSpan.FromMinutes(5),
            AssertionLifetime = TimeSpan.FromMinutes(5),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

    private static List<ServiceProviderCertificate>? BuildCertificates(
        System.Security.Cryptography.X509Certificates.X509Certificate2? signingCertificate,
        System.Security.Cryptography.X509Certificates.X509Certificate2[]? encryptionCertificates)
    {
        if (signingCertificate == null && encryptionCertificates == null)
        {
            return null;
        }

        var certs = new List<ServiceProviderCertificate>();
        if (signingCertificate != null)
        {
            certs.Add(new ServiceProviderCertificate { Certificate = signingCertificate, Use = KeyUse.Signing });
        }

        if (encryptionCertificates != null)
        {
            foreach (var cert in encryptionCertificates)
            {
                certs.Add(new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Encryption });
            }
        }

        return certs;
    }


    public string AuthNRequestXml(
        DateTimeOffset? issueInstant = null,
        Uri? destination = null,
        Uri? acsUrl = null,
        int? acsIndex = null,
        string? version = null,
        bool forceAuthn = false,
        bool isPassive = false,
        string? requestId = null,
        string? issuer = null,
        string? requestedAuthnContext = null,
        string? nameIdFormat = null,
        string? spNameQualifier = null,
        SamlBinding samlBinding = SamlBinding.HttpPost
        )
    {
        var id = requestId ?? data.RequestId;
        var issuerValue = issuer ?? data.EntityId;

        var acsAttributes = "";
        if (acsUrl != null && acsIndex != null)
        {
            acsAttributes = $"""AssertionConsumerServiceURL="{acsUrl}" AssertionConsumerServiceIndex="{acsIndex}" """;
        }
        else if (acsUrl != null)
        {
            acsAttributes = $"""AssertionConsumerServiceURL="{acsUrl}" """;
        }
        else if (acsIndex != null)
        {
            acsAttributes = $"""AssertionConsumerServiceIndex="{acsIndex}" """;
        }
        else
        {
            acsAttributes = $"""AssertionConsumerServiceURL="{data.AcsUrl}" """;
        }

        var nameIdPolicyElement = "";
        if (nameIdFormat != null || spNameQualifier != null)
        {
            var formatAttr = nameIdFormat != null ? $"""Format="{nameIdFormat}" """ : "";
            var spNameQualifierAttr = spNameQualifier != null ? $"""SPNameQualifier="{spNameQualifier}" """ : "";
            nameIdPolicyElement = $"""<samlp:NameIDPolicy {formatAttr}{spNameQualifierAttr}/>""";
        }

        return $"""
                                                  <?xml version="1.0" encoding="UTF-8"?>
                                                   <samlp:AuthnRequest
                                                       ID="{id}"
                                                       Version="{version ?? "2.0"}"
                                                       Destination="{destination}"
                                                       IssueInstant="{new DateTimeUtc((issueInstant ?? data.Now).Ticks)}"
                                                       ProtocolBinding="{samlBinding.ToUrn()}"
                                                       {acsAttributes}
                                                       ForceAuthn="{XmlConvert.ToString(forceAuthn)}"
                                                       IsPassive="{XmlConvert.ToString(isPassive)}"
                                                       xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                                       xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
                                                       <saml:Issuer>{issuerValue}</saml:Issuer>
                                                       {requestedAuthnContext}
                                                       {nameIdPolicyElement}
                                                   </samlp:AuthnRequest>
                                               """.Trim();
    }

    public string LogoutRequestXml(
        DateTimeOffset? issueInstant = null,
        Uri? destination = null,
        string? version = null,
        string? requestId = null,
        string? issuer = null,
        string? nameId = null,
        string? nameIdFormat = null,
        string? sessionIndex = "12345",
        DateTimeOffset? notOnOrAfter = null)
    {
        var id = requestId ?? data.RequestId;
        var issuerValue = issuer ?? data.EntityId;
        var nameIdValue = nameId ?? "user@example.com";
        var nameIdFormatValue = nameIdFormat ?? SamlConstants.NameIdentifierFormats.EmailAddress;

        var sessionIndexElement = sessionIndex != null
            ? $"<samlp:SessionIndex>{sessionIndex}</samlp:SessionIndex>"
            : "";

        var notOnOrAfterAttr = notOnOrAfter.HasValue
            ? $"NotOnOrAfter=\"{new DateTimeUtc(notOnOrAfter.Value.Ticks)}\""
            : "";

        return $"""
                                                  <?xml version="1.0" encoding="UTF-8"?>
                                                   <samlp:LogoutRequest
                                                       ID="{id}"
                                                       Version="{version ?? "2.0"}"
                                                       Destination="{destination}"
                                                       IssueInstant="{new DateTimeUtc((issueInstant ?? data.Now).Ticks)}"
                                                       {notOnOrAfterAttr}
                                                       xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                                       xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
                                                       <saml:Issuer>{issuerValue}</saml:Issuer>
                                                       <saml:NameID Format="{nameIdFormatValue}">{nameIdValue}</saml:NameID>
                                                   {sessionIndexElement}
                                                    </samlp:LogoutRequest>
                                                """.Trim();
    }

    public string LogoutResponseXml(
        string inResponseTo,
        string statusCode = "urn:oasis:names:tc:SAML:2.0:status:Success",
        string? subStatusCode = null,
        DateTimeOffset? issueInstant = null,
        Uri? destination = null,
        string? responseId = null,
        string? issuer = null)
    {
        var id = responseId ?? $"_response-{Guid.NewGuid():N}";
        var issuerValue = issuer ?? data.EntityId;

        var subStatusElement = subStatusCode != null
            ? $"""<samlp:StatusCode Value="{subStatusCode}" />"""
            : "";

        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <samlp:LogoutResponse
                    ID="{id}"
                    Version="2.0"
                    IssueInstant="{new DateTimeUtc((issueInstant ?? data.Now).Ticks)}"
                    Destination="{destination}"
                    InResponseTo="{inResponseTo}"
                    xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                    xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
                    <saml:Issuer>{issuerValue}</saml:Issuer>
                    <samlp:Status>
                        <samlp:StatusCode Value="{statusCode}">
                            {subStatusElement}
                        </samlp:StatusCode>
                    </samlp:Status>
                </samlp:LogoutResponse>
                """.Trim();
    }
}

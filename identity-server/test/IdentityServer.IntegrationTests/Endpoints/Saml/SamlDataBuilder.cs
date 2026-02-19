// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

internal class SamlDataBuilder(SamlData data)
{
    public SamlServiceProvider SamlServiceProvider(
        System.Security.Cryptography.X509Certificates.X509Certificate2? signingCertificate = null,
        bool requireSignedAuthnRequests = false,
        System.Security.Cryptography.X509Certificates.X509Certificate2[]? encryptionCertificates = null,
        bool encryptAssertions = false,
        string? entityId = null) => new SamlServiceProvider
        {
            EntityId = entityId ?? data.EntityId,
            DisplayName = "Example SP",
            Description = "Example SP",
            Enabled = true,
            RequireSignedAuthnRequests = requireSignedAuthnRequests || signingCertificate != null,
            SigningCertificates = signingCertificate != null ? [signingCertificate] : null,
            EncryptionCertificates = encryptionCertificates,
            EncryptAssertions = encryptAssertions,
            AssertionConsumerServiceUrls = [data.AcsUrl],
            AssertionConsumerServiceBinding = SamlBinding.HttpPost,
            SingleLogoutServiceUrl = new SamlEndpointType { Location = data.SingleLogoutServiceUrl, Binding = SamlBinding.HttpRedirect },
            RequestMaxAge = TimeSpan.FromMinutes(5),
            ClockSkew = TimeSpan.FromMinutes(5)
        };


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
        string? spNameQualifier = null
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
                                                       IssueInstant="{(issueInstant ?? data.Now):yyyy-MM-ddTHH:mm:ssZ}"
                                                       ProtocolBinding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect"
                                                       {acsAttributes}
                                                       ForceAuthn="{forceAuthn}"
                                                       IsPassive="{isPassive}"
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
            ? $"NotOnOrAfter=\"{notOnOrAfter.Value:yyyy-MM-ddTHH:mm:ssZ}\""
            : "";

        return $"""
                                                  <?xml version="1.0" encoding="UTF-8"?>
                                                   <samlp:LogoutRequest
                                                       ID="{id}"
                                                       Version="{version ?? "2.0"}"
                                                       Destination="{destination}"
                                                       IssueInstant="{(issueInstant ?? data.Now):yyyy-MM-ddTHH:mm:ssZ}"
                                                       {notOnOrAfterAttr}
                                                       xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                                       xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
                                                       <saml:Issuer>{issuerValue}</saml:Issuer>
                                                       <saml:NameID Format="{nameIdFormatValue}">{nameIdValue}</saml:NameID>
                                                       {sessionIndexElement}
                                                   </samlp:LogoutRequest>
                                               """.Trim();
    }
}

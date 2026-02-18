// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class AuthNRequestParser : SamlProtocolMessageParser
{
    private readonly ILogger<AuthNRequestParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthNRequestParser"/> class.
    /// </summary>
    public AuthNRequestParser(ILogger<AuthNRequestParser> logger) => _logger = logger;

    internal AuthNRequest Parse(XDocument doc)
    {
        try
        {
            var ns = XNamespace.Get(SamlConstants.Namespaces.Protocol);
            var assertionNs = XNamespace.Get(SamlConstants.Namespaces.Assertion);

            var root = doc.Root;
            if (root?.Name != ns + SamlConstants.AuthenticationRequestAttributes.RootElementName)
            {
                throw new FormatException(
                    $"Root element is not AuthnRequest. Found: {root?.Name}");
            }

            var request = new AuthNRequest
            {
                Id = GetRequiredAttribute(root, AuthNRequest.AttributeNames.Id),
                Version = GetRequiredAttribute(root, AuthNRequest.AttributeNames.Version),
                IssueInstant = ParseDateTime(root, AuthNRequest.AttributeNames.IssueInstant),
                Destination = GetOptionalAttribute(root, AuthNRequest.AttributeNames.Destination) is { } dest ? new Uri(dest) : null,
                Consent = GetOptionalAttribute(root, AuthNRequest.AttributeNames.Consent),
                Issuer = ParseIssuerValue(root, assertionNs, "AuthnRequest"),
                ForceAuthn = ParseBooleanAttribute(root, AuthNRequest.AttributeNames.ForceAuthn, false),
                IsPassive = ParseBooleanAttribute(root, AuthNRequest.AttributeNames.IsPassive, false),
                AssertionConsumerServiceUrl =
                    GetOptionalAttribute(root, AuthNRequest.AttributeNames.AssertionConsumerServiceUrl) is { } acsUrl ? new Uri(acsUrl) : null,
                AssertionConsumerServiceIndex =
                    ParseIntegerAttribute(root, AuthNRequest.AttributeNames.AssertionConsumerServiceIndex),
                ProtocolBinding =
                    SamlBindingExtensions.FromUrnOrDefault(GetOptionalAttribute(root, AuthNRequest.AttributeNames.ProtocolBinding))
            };

            // Parse optional elements
            // request.Subject = ParseSubject(root, assertionNs);
            request.NameIdPolicy = ParseNameIdPolicy(root, ns);
            // request.Conditions = ParseConditions(root, assertionNs);
            request.RequestedAuthnContext = ParseRequestedAuthnContext(root, ns);
            // request.Scoping = ParseScoping(root, ns);

            _logger.ParsedAuthenticationRequest(request.Id, request.Issuer);

            return request;
        }
        catch (XmlException ex)
        {
            _logger.FailedToParseAuthNRequest(ex, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.UnexpectedErrorParsingAuthNRequest(ex);
            throw;
        }
    }

    private static NameIdPolicy? ParseNameIdPolicy(XElement root, XNamespace ns)
    {
        var nameIdPolicyElement = root.Element(ns + AuthNRequest.ElementNames.NameIdPolicy);
        if (nameIdPolicyElement == null)
        {
            return null;
        }

        var format = GetOptionalAttribute(nameIdPolicyElement, NameIdPolicy.AttributeNames.Format);
        var spNameQualifier = GetOptionalAttribute(nameIdPolicyElement, NameIdPolicy.AttributeNames.SPNameQualifier);

        // If element exists but all attributes are null/default, still return object
        // to indicate element was present (SP may want default behavior explicitly)
        return new NameIdPolicy
        {
            Format = string.IsNullOrWhiteSpace(format) ? null : format.Trim(),
            SPNameQualifier = string.IsNullOrWhiteSpace(spNameQualifier) ? null : spNameQualifier.Trim()
        };
    }

    private static int? ParseIntegerAttribute(XElement element, string attributeName)
    {
        var value = GetOptionalAttribute(element, attributeName);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static RequestedAuthnContext? ParseRequestedAuthnContext(XElement root, XNamespace ns)
    {
        var requestedAuthnContextElement = root.Element(ns + AuthNRequest.ElementNames.RequestedAuthnContext);
        if (requestedAuthnContextElement == null)
        {
            return null;
        }

        // Parse Comparison attribute (defaults to "exact" per spec)
        var comparisonAttr = requestedAuthnContextElement.Attribute(RequestedAuthnContext.AttributeNames.Comparison)?.Value;
        var comparison = AuthnContextComparisonExtensions.Parse(comparisonAttr);

        var assertionNs = XNamespace.Get(SamlConstants.Namespaces.Assertion);
        var classRefs = requestedAuthnContextElement
            .Elements(assertionNs + RequestedAuthnContext.ElementNames.AuthnContextClassRef)
            .Select(e => e.Value?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .ToList();

        if (classRefs.Count == 0)
        {
            throw new InvalidOperationException("No AuthnContextClassRef element found in requestedAuthnContext");
        }

        return new RequestedAuthnContext
        {
            AuthnContextClassRefs = classRefs.AsReadOnly(),
            Comparison = comparison
        };
    }
}

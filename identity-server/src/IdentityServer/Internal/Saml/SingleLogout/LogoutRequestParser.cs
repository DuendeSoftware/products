// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

/// <summary>
/// Parses SAML LogoutRequest messages.
/// </summary>
internal class LogoutRequestParser(ILogger<LogoutRequestParser> logger) : SamlProtocolMessageParser
{
    /// <summary>
    /// Parses a LogoutRequest from XML.
    /// </summary>
    internal LogoutRequest Parse(XDocument doc)
    {
        try
        {
            var protocolNs = XNamespace.Get(SamlConstants.Namespaces.Protocol);
            var assertionNs = XNamespace.Get(SamlConstants.Namespaces.Assertion);

            var root = doc.Root;
            if (root?.Name != protocolNs + LogoutRequest.ElementNames.RootElement)
            {
                throw new FormatException($"Root element is not LogoutRequest. Found: {root?.Name}");
            }

            var request = new LogoutRequest
            {
                Id = GetRequiredAttribute(root, LogoutRequest.AttributeNames.Id),
                Version = GetRequiredAttribute(root, LogoutRequest.AttributeNames.Version),
                IssueInstant = ParseDateTime(root, LogoutRequest.AttributeNames.IssueInstant),
                Destination = GetOptionalAttribute(root, LogoutRequest.AttributeNames.Destination) is { } dest ? new Uri(dest) : null,
                Issuer = ParseIssuerValue(root, assertionNs, "LogoutRequest"),
                NameId = ParseNameIdAsIdentifier(root, assertionNs),
                SessionIndex = ParseSessionIndex(root, protocolNs),
                Reason = ParseReason(GetOptionalAttribute(root, LogoutRequest.AttributeNames.Reason)),
            };

            var notOnOrAfterAttr = root.Attribute(LogoutRequest.AttributeNames.NotOnOrAfter)?.Value;
            if (!string.IsNullOrEmpty(notOnOrAfterAttr))
            {
                request.NotOnOrAfter = DateTime.Parse(notOnOrAfterAttr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            }

            logger.ParsedLogoutRequest(LogLevel.Debug, request.Id, request.Issuer, request.SessionIndex);

            return request;
        }
        catch (XmlException ex)
        {
            logger.FailedToParseLogoutRequest(ex, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.UnexpectedErrorParsingLogoutRequest(ex);
            throw;
        }
    }



    private static NameIdentifier ParseNameIdAsIdentifier(XElement root, XNamespace assertionNs)
    {
        var nameIdElement = root.Element(assertionNs + LogoutRequest.ElementNames.NameID);
        if (nameIdElement == null)
        {
            throw new InvalidOperationException("NameID element is required in LogoutRequest");
        }

        var nameId = nameIdElement.Value?.Trim();
        if (string.IsNullOrEmpty(nameId))
        {
            throw new InvalidOperationException("NameID element cannot be empty");
        }

        var format = nameIdElement.Attribute(NameIdPolicy.AttributeNames.Format)?.Value;
        var nameQualifier = nameIdElement.Attribute("NameQualifier")?.Value;
        var spNameQualifierAttr = nameIdElement.Attribute(NameIdPolicy.AttributeNames.SPNameQualifier);

        string? spNameQualifier = null;
        if (spNameQualifierAttr != null && !string.IsNullOrWhiteSpace(spNameQualifierAttr.Value))
        {
            spNameQualifier = spNameQualifierAttr.Value;
        }

        return new NameIdentifier
        {
            Value = nameId,
            Format = format,
            NameQualifier = nameQualifier,
            SPNameQualifier = spNameQualifier
        };
    }

    private static string ParseSessionIndex(XElement root, XNamespace protocolNs)
    {
        var sessionIndexElement = root.Element(protocolNs + LogoutRequest.ElementNames.SessionIndex);
        if (sessionIndexElement == null)
        {
            throw new InvalidOperationException("SessionIndex element is required in LogoutRequest");
        }

        var sessionIndex = sessionIndexElement.Value.Trim();
        if (string.IsNullOrEmpty(sessionIndex))
        {
            throw new InvalidOperationException("SessionIndex element cannot be empty");
        }

        return sessionIndex;
    }

    private static LogoutReason? ParseReason(string? reasonUrn) => reasonUrn switch
    {
        SamlConstants.LogoutReasons.User => LogoutReason.User,
        SamlConstants.LogoutReasons.Admin => LogoutReason.Admin,
        SamlConstants.LogoutReasons.GlobalTimeout => LogoutReason.GlobalTimeout,
        _ => null
    };
}

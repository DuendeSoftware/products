// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Xml.Linq;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Base class for SAML protocol message parsers.
/// Provides common XML parsing and validation utilities.
/// </summary>
internal abstract class SamlProtocolMessageParser
{
    protected static string GetRequiredAttribute(XElement element, XName attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Required attribute '{attributeName}' is missing or empty");
        }

        return value;
    }

    protected static string? GetOptionalAttribute(XElement element, XName attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    protected static DateTime ParseDateTime(XElement element, XName attributeName)
    {
        var value = GetRequiredAttribute(element, attributeName);
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            throw new FormatException($"Invalid DateTime format for attribute '{attributeName}': {value}");
        }

        return result;
    }

    protected static bool ParseBooleanAttribute(XElement element, XName attributeName, bool defaultValue)
    {
        var value = GetOptionalAttribute(element, attributeName);
        if (value == null)
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var result))
        {
            return result;
        }

        throw new FormatException($"Invalid boolean format for attribute '{attributeName}': {value}");
    }

    protected static string ParseIssuerValue(XElement root, XNamespace assertionNs, string messageType)
    {
        var issuerElement = root.Element(assertionNs + "Issuer");
        if (issuerElement == null)
        {
            throw new InvalidOperationException($"Issuer element is required in {messageType}");
        }

        var issuer = issuerElement.Value?.Trim();
        if (string.IsNullOrEmpty(issuer))
        {
            throw new InvalidOperationException("Issuer element cannot be empty");
        }

        return issuer;
    }
}

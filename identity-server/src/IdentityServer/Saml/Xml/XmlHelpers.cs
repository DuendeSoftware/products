// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography;
using System.Xml;
using Duende.IdentityServer.Saml.Common;
using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityServer.Saml.Xml;

/// <summary>
/// Xml utilities
/// </summary>
public static class XmlHelpers
{
    /// <summary>
    /// Create a valid xs:ID
    /// </summary>
    /// <returns>Id as string</returns>
    public static string CreateId()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return FormatId(bytes);
    }

    // Split to separate function to enable testing of formatting.
    internal static string FormatId(byte[] bytes)
    {
        // Ensure starting char will be a letter.
        bytes[0] = (byte)(bytes[0] & 0x7F);

        return Base64UrlTextEncoder.Encode(bytes);
    }

    /// <summary>
    /// Get an Xml traverser for an XmlDocument
    /// </summary>
    /// <param name="xmlElement">Source XmlElement. Typically the document element</param>
    /// <returns>XmlTraverser located at DocumentElement</returns>
    public static XmlTraverser GetXmlTraverser(this XmlElement xmlElement)
        => new(xmlElement ?? throw new ArgumentException("DocumentElement cannot be null"));

    /// <summary>
    /// Sets an attribute if the value is not null.
    /// </summary>
    /// <param name="element">Element to set attribute on.</param>
    /// <param name="name">Name of attribute</param>
    /// <param name="value">String value. If null, no attribute is set/created</param>
    public static void SetAttributeIfValue(this XmlElement element, string name, string? value)
    {
        if (value != null)
        {
            element.SetAttribute(name, value);
        }
    }

    /// <summary>
    /// Sets a DateTimeUtc attribute in the correct format.
    /// </summary>
    /// <param name="element">Element to set attribute on.</param>
    /// <param name="name">Name of attribute</param>
    /// <param name="value">DateTimeUtc value.</param>
    public static void SetAttribute(this XmlElement element, string name, DateTimeUtc value) =>
        element.SetAttribute(name, value.ToString());

    /// <summary>
    /// Sets a DateTimeUtc attribute in the correct format, if the value is not null (HasValue)
    /// </summary>
    /// <param name="element">Element to set attribute on.</param>
    /// <param name="name">Name of attribute</param>
    /// <param name="value">DateTimeUtc value.</param>
    public static void SetAttributeIfValue(this XmlElement element, string name, DateTimeUtc? value)
    {
        if (value.HasValue)
        {
            element.SetAttribute(name, value.Value);
        }
    }

    /// <summary>
    /// Sets a TimeSpan attribute in the correct format.
    /// </summary>
    /// <param name="element">Element to set attribute on.</param>
    /// <param name="name">Name of attribute</param>
    /// <param name="value">TimeSpan value.</param>
    public static void SetAttribute(this XmlElement element, string name, TimeSpan value) =>
        element.SetAttribute(name, XmlConvert.ToString(value));

    /// <summary>
    /// Sets a TimeSpan attribute in the correct format, if the value is not null (HasValue)
    /// </summary>
    /// <param name="element">Element to set attribute on.</param>
    /// <param name="name">Name of attribute</param>
    /// <param name="value">TimeSpan value.</param>
    public static void SetAttributeIfValue(this XmlElement element, string name, TimeSpan? value)
    {
        if (value.HasValue)
        {
            element.SetAttribute(name, value.Value);
        }
    }
}

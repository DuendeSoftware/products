// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.CodeAnalysis;
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

    extension(XmlElement element)
    {
        /// <summary>
        /// Get an Xml traverser for an XmlDocument
        /// </summary>
        /// <returns>XmlTraverser located at DocumentElement</returns>
        [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Changing this extension method to a property would be a breaking API change")]
        public XmlTraverser GetXmlTraverser()
            => new(element ?? throw new ArgumentException("DocumentElement cannot be null"));

        /// <summary>
        /// Sets an attribute if the value is not null.
        /// </summary>
        /// <param name="name">Name of attribute</param>
        /// <param name="value">String value. If null, no attribute is set/created</param>
        public void SetAttributeIfValue(string name, string? value)
        {
            if (value != null)
            {
                element.SetAttribute(name, value);
            }
        }

        /// <summary>
        /// Sets a DateTimeUtc attribute in the correct format.
        /// </summary>
        /// <param name="name">Name of attribute</param>
        /// <param name="value">DateTimeUtc value.</param>
        public void SetAttribute(string name, DateTimeUtc value) =>
            element.SetAttribute(name, value.ToString());

        /// <summary>
        /// Sets a DateTimeUtc attribute in the correct format, if the value is not null (HasValue)
        /// </summary>
        /// <param name="name">Name of attribute</param>
        /// <param name="value">DateTimeUtc value.</param>
        public void SetAttributeIfValue(string name, DateTimeUtc? value)
        {
            if (value.HasValue)
            {
                element.SetAttribute(name, value.Value);
            }
        }

        /// <summary>
        /// Sets a TimeSpan attribute in the correct format.
        /// </summary>
        /// <param name="name">Name of attribute</param>
        /// <param name="value">TimeSpan value.</param>
        public void SetAttribute(string name, TimeSpan value) =>
            element.SetAttribute(name, XmlConvert.ToString(value));

        /// <summary>
        /// Sets a TimeSpan attribute in the correct format, if the value is not null (HasValue)
        /// </summary>
        /// <param name="name">Name of attribute</param>
        /// <param name="value">TimeSpan value.</param>
        public void SetAttributeIfValue(string name, TimeSpan? value)
        {
            if (value.HasValue)
            {
                element.SetAttribute(name, value.Value);
            }
        }
    }
}

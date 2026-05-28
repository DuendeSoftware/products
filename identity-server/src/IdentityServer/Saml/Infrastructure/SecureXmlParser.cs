// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using System.Xml.Linq;

namespace Duende.IdentityServer.Saml.Infrastructure;

/// <summary>
/// Provides secure XML parsing with hardened settings to prevent common XML attacks.
/// </summary>
/// <remarks>
/// This class protects against:
/// - XXE (XML External Entity) attacks
/// - DTD (Document Type Definition) attacks
/// - Billion laughs attack (entity expansion)
/// - Resource exhaustion attacks
/// </remarks>
internal static class SecureXmlParser
{
    /// <summary>
    /// Default maximum allowed size for SAML messages (1MB).
    /// </summary>
    internal const int DefaultMaxMessageSize = 1_048_576; // 1MB

    /// <summary>
    /// Creates secure XML reader settings configured to prevent common XML attacks.
    /// </summary>
    private static XmlReaderSettings CreateSecureSettings(int maxMessageSize) => new()
    {
        // Prohibit DTD processing to prevent DTD-based attacks
        DtdProcessing = DtdProcessing.Prohibit,

        // Disable external entity resolution to prevent XXE attacks
        XmlResolver = null,

        // Prevent entity expansion attacks (billion laughs)
        MaxCharactersFromEntities = 0,

        // Limit document size to prevent resource exhaustion
        MaxCharactersInDocument = maxMessageSize,

        // Preserve comments because the exc-c14n#WithComments transform includes
        // them in the signed data. Stripping comments would break signature validation.
        IgnoreComments = false,

        // Ignore processing instructions to reduce attack surface
        IgnoreProcessingInstructions = true,

        // Validate well-formed XML
        ConformanceLevel = ConformanceLevel.Document
    };

    internal static XElement LoadXElement(Stream input)
        => LoadXElement(input, DefaultMaxMessageSize);

    internal static XElement LoadXElement(Stream input, int maxMessageSize)
    {
        try
        {
            var streamReader = new StreamReader(input);
            using var xmlReader = XmlReader.Create(streamReader, CreateSecureSettings(maxMessageSize));
            return XElement.Load(xmlReader);
        }
        catch (XmlException ex)
        {
            throw new XmlException(
                "Failed to parse XML document with secure settings. " +
                "The document may contain prohibited constructs (DTD, external entities) or be malformed.",
                ex);
        }
    }

    internal static XElement LoadXElement(string xml)
        => LoadXElement(xml, DefaultMaxMessageSize);

    /// <summary>
    /// Loads an XElement from a string with secure settings.
    /// </summary>
    /// <param name="xml">The XML string to parse</param>
    /// <param name="maxMessageSize">Maximum allowed document size in characters</param>
    /// <returns>A parsed XElement</returns>
    /// <exception cref="ArgumentNullException">Thrown when xml is null or empty</exception>
    /// <exception cref="XmlException">Thrown when XML is malformed or violates security constraints</exception>
    internal static XElement LoadXElement(string xml, int maxMessageSize)
    {
        if (string.IsNullOrEmpty(xml))
        {
            throw new ArgumentNullException(nameof(xml), "XML content cannot be null or empty");
        }

        // Check size before parsing
        if (xml.Length > maxMessageSize)
        {
            throw new XmlException(
                $"XML document exceeds maximum allowed size of {maxMessageSize} characters. " +
                $"Actual size: {xml.Length} characters.");
        }

        try
        {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, CreateSecureSettings(maxMessageSize));

            return XElement.Load(xmlReader);
        }
        catch (XmlException ex)
        {
            throw new XmlException(
                "Failed to parse XML document with secure settings. " +
                "The document may contain prohibited constructs (DTD, external entities) or be malformed.",
                ex);
        }
    }

    internal static XDocument LoadXDocument(Stream input)
        => LoadXDocument(input, DefaultMaxMessageSize);

    internal static XDocument LoadXDocument(Stream input, int maxMessageSize)
    {
        try
        {
            using var xmlReader = XmlReader.Create(input, CreateSecureSettings(maxMessageSize));
            return XDocument.Load(xmlReader);
        }
        catch (XmlException ex)
        {
            throw new XmlException(
                "Failed to parse XML document with secure settings. " +
                "The document may contain prohibited constructs (DTD, external entities) or be malformed.",
                ex);
        }
    }

    internal static XDocument LoadXDocument(string xml)
        => LoadXDocument(xml, DefaultMaxMessageSize);

    /// <summary>
    /// Loads an XDocument from a string with secure settings.
    /// </summary>
    /// <param name="xml">The XML string to parse</param>
    /// <param name="maxMessageSize">Maximum allowed document size in characters</param>
    /// <returns>A parsed XDocument</returns>
    /// <exception cref="ArgumentNullException">Thrown when xml is null or empty</exception>
    /// <exception cref="XmlException">Thrown when XML is malformed or violates security constraints</exception>
    internal static XDocument LoadXDocument(string xml, int maxMessageSize)
    {
        if (string.IsNullOrEmpty(xml))
        {
            throw new ArgumentNullException(nameof(xml), "XML content cannot be null or empty");
        }

        // Check size before parsing
        if (xml.Length > maxMessageSize)
        {
            throw new XmlException(
                $"XML document exceeds maximum allowed size of {maxMessageSize} characters. " +
                $"Actual size: {xml.Length} characters.");
        }

        try
        {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, CreateSecureSettings(maxMessageSize));

            return XDocument.Load(xmlReader);
        }
        catch (XmlException ex)
        {
            throw new XmlException(
                "Failed to parse XML document with secure settings. " +
                "The document may contain prohibited constructs (DTD, external entities) or be malformed.",
                ex);
        }
    }

    internal static XmlDocument LoadXmlDocument(string xml)
        => LoadXmlDocument(xml, DefaultMaxMessageSize);

    internal static XmlDocument LoadXmlDocument(string xml, int maxMessageSize)
    {
        if (string.IsNullOrEmpty(xml))
        {
            throw new ArgumentNullException(nameof(xml), "XML content cannot be null or empty");
        }

        if (xml.Length > maxMessageSize)
        {
            throw new XmlException(
                $"XML document exceeds maximum allowed size of {maxMessageSize} characters. " +
                $"Actual size: {xml.Length} characters.");
        }

        try
        {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, CreateSecureSettings(maxMessageSize));

            var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            doc.Load(xmlReader);

            return doc;
        }
        catch (XmlException ex)
        {
            throw new XmlException(
                "Failed to parse XML document with secure settings. " +
                "The document may contain prohibited constructs (DTD, external entities) or be malformed.",
                ex);
        }
    }
}

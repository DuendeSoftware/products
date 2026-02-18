// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using System.Xml.Linq;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

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
    /// Maximum allowed size for SAML messages (1MB).
    /// </summary>
    internal const int MaxMessageSize = 1048576; // 1MB

    /// <summary>
    /// Secure XML reader settings configured to prevent common XML attacks.
    /// </summary>
    private static readonly XmlReaderSettings SecureSettings = new()
    {
        // Prohibit DTD processing to prevent DTD-based attacks
        DtdProcessing = DtdProcessing.Prohibit,

        // Disable external entity resolution to prevent XXE attacks
        XmlResolver = null,

        // Prevent entity expansion attacks (billion laughs)
        MaxCharactersFromEntities = 0,

        // Limit document size to prevent resource exhaustion
        MaxCharactersInDocument = MaxMessageSize,

        // Ignore comments to prevent comment injection attacks
        IgnoreComments = true,

        // Ignore processing instructions to reduce attack surface
        IgnoreProcessingInstructions = true,

        // Validate well-formed XML
        ConformanceLevel = ConformanceLevel.Document
    };

    internal static XDocument LoadXDocument(Stream input)
    {
        try
        {
            using var xmlReader = XmlReader.Create(input, SecureSettings);
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
    {
        if (string.IsNullOrEmpty(xml))
        {
            throw new ArgumentNullException(nameof(xml), "XML content cannot be null or empty");
        }

        if (xml.Length > MaxMessageSize)
        {
            throw new XmlException(
                $"XML document exceeds maximum allowed size of {MaxMessageSize} bytes. " +
                $"Actual size: {xml.Length} bytes.");
        }

        try
        {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, SecureSettings);

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

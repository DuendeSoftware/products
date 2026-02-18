// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal static class XmlSignatureHelper
{
    internal static string SignResponse(XElement responseElement, X509Certificate2 certificate)
    {
        var xmlDoc = ConvertToXmlDocument(responseElement);

        var docElement = xmlDoc.DocumentElement;
        if (docElement?.LocalName != "Response")
        {
            throw new ArgumentException("XML must contain a Response element");
        }

        SignElement(xmlDoc, docElement, certificate);
        return xmlDoc.OuterXml;
    }

    internal static string SignProtocolElement(XElement protocolElement, X509Certificate2 certificate)
    {
        var xmlDoc = ConvertToXmlDocument(protocolElement);

        var docElement = xmlDoc.DocumentElement;
        if (docElement == null)
        {
            throw new ArgumentException("XML must contain a root element");
        }

        SignElement(xmlDoc, docElement, certificate);
        return xmlDoc.OuterXml;
    }

    internal static string SignAssertionInResponse(XElement responseElement, X509Certificate2 certificate)
    {
        var xmlDoc = ConvertToXmlDocument(responseElement);

        // Find the Assertion element
        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        nsmgr.AddNamespace("samlp", SamlConstants.Namespaces.Protocol);

        var assertionNode = xmlDoc.SelectSingleNode("//saml:Assertion", nsmgr);
        if (assertionNode is not XmlElement assertionElement)
        {
            throw new ArgumentException("Response must contain an Assertion element");
        }

        SignElement(xmlDoc, assertionElement, certificate);
        return xmlDoc.OuterXml;
    }

    internal static string SignBoth(XElement responseElement, X509Certificate2 certificate)
    {
        // First sign the assertion
        var xmlAfterAssertionSigned = SignAssertionInResponse(responseElement, certificate);

        // Convert back to XElement and then sign the response
        var xmlDoc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        xmlDoc.LoadXml(xmlAfterAssertionSigned);

        var docElement = xmlDoc.DocumentElement;
        if (docElement?.LocalName != "Response")
        {
            throw new ArgumentException("XML must contain a Response element");
        }

        SignElement(xmlDoc, docElement, certificate);
        return xmlDoc.OuterXml;
    }

    private static void SignElement(
        XmlDocument xmlDoc,
        XmlElement elementToSign,
        X509Certificate2 certificate)
    {
        // Validate element has ID attribute (required for SAML signing)
        var idAttribute = elementToSign.GetAttribute("ID");
        if (string.IsNullOrEmpty(idAttribute))
        {
            throw new ArgumentException("Element to sign must have an ID attribute");
        }

        // Get private key
        var privateKey = certificate.GetRSAPrivateKey();
        if (privateKey == null)
        {
            throw new CryptographicException("Cannot get private key from certificate");
        }

        // Create a custom SignedXml that knows how to resolve ID attributes
        var signedXml = new SignedXml(xmlDoc)
        {
            SigningKey = privateKey
        };

        // Set canonicalization method for SignedInfo
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        // Create reference to the element (using its ID)
        var reference = new Reference($"#{idAttribute}")
        {
            DigestMethod = SignedXml.XmlDsigSHA256Url
        };

        // Add transforms
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());

        signedXml.AddReference(reference);

        // Add certificate to KeyInfo
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        // Compute signature
        signedXml.ComputeSignature();

        // Get signature element
        var signatureElement = signedXml.GetXml();

        // Insert signature after Issuer element (per SAML spec)
        InsertSignatureAfterIssuer(elementToSign, signatureElement);
    }

    /// <summary>
    /// Inserts signature element after Issuer element (SAML requirement)
    /// </summary>
    private static void InsertSignatureAfterIssuer(
        XmlElement parentElement,
        XmlElement signatureElement)
    {
        // Find Issuer element
        var issuerElement = parentElement.SelectSingleNode("*[local-name()='Issuer']");

        if (issuerElement != null && issuerElement.NextSibling != null)
        {
            // Insert after Issuer
            parentElement.InsertAfter(signatureElement, issuerElement);
        }
        else
        {
            // No Issuer or no next sibling - insert as first child
            if (parentElement.FirstChild != null)
            {
                parentElement.InsertBefore(signatureElement, parentElement.FirstChild);
            }
            else
            {
                parentElement.AppendChild(signatureElement);
            }
        }
    }

    /// <summary>
    /// Converts XElement to XmlDocument, preserving namespace prefixes
    /// </summary>
    private static XmlDocument ConvertToXmlDocument(XElement element)
    {
        var xmlDoc = new XmlDocument
        {
            PreserveWhitespace = true, // Important for signatures
            XmlResolver = null // Disable external entity resolution (XXE protection)
        };

        using var reader = element.CreateReader();
        xmlDoc.Load(reader);

        return xmlDoc;
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal class SamlAssertionEncryptor(TimeProvider timeProvider, ILogger<SamlAssertionEncryptor> logger)
{
    internal string EncryptAssertion(string responseXml, SamlServiceProvider serviceProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseXml);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var encryptionCertificate = serviceProvider.EncryptionCertificates?.FirstOrDefault(cert => IsCertificateValid(cert, serviceProvider.EntityId));
        if (encryptionCertificate == null)
        {
            throw new InvalidOperationException($"No valid encryption certificate found for {serviceProvider.EntityId}. Certificates may be expired, not yet valid, or lacking required RSA keys.");
        }

        var doc = SecureXmlParser.LoadXmlDocument(responseXml);
        var assertion = FindAssertion(doc);
        if (assertion == null)
        {
            throw new InvalidOperationException($"SAML Response does not contain an Assertion element for {serviceProvider.EntityId}");
        }

        logger.EncryptingAssertion(LogLevel.Debug, serviceProvider.EntityId);

        try
        {
            var encryptedAssertion = EncryptAssertionXml(assertion, encryptionCertificate, doc);

            ReplaceAssertionWithEncrypted(assertion, encryptedAssertion);

            logger.AssertionEncryptedSuccessfully(LogLevel.Debug, serviceProvider.EntityId);

            return doc.OuterXml;
        }
        catch (Exception ex)
        {
            logger.FailedToEncryptAssertion(ex, serviceProvider.EntityId, ex.Message);
            throw;
        }
    }

    private bool IsCertificateValid(X509Certificate2 certificate, string serviceProviderEntityId)
    {
        var now = timeProvider.GetUtcNow();
        if (certificate.NotAfter < now)
        {
            logger.CertificateExpired(LogLevel.Error, serviceProviderEntityId, certificate.NotAfter);
            return false;
        }

        if (certificate.NotBefore > now)
        {
            logger.CertificateNotYetValid(LogLevel.Error, serviceProviderEntityId, certificate.NotBefore);
            return false;
        }

        using var publicKey = certificate.GetRSAPublicKey();
        if (publicKey == null)
        {
            logger.CertificateHasNoPublicRsaKey(LogLevel.Error, serviceProviderEntityId);
            return false;
        }

        if (publicKey.KeySize < 2048)
        {
            logger.CertificateWeakKeySize(LogLevel.Error, serviceProviderEntityId, publicKey.KeySize);
            return false;
        }

        logger.CertificateValidated(LogLevel.Debug, serviceProviderEntityId, certificate.NotAfter);

        return true;
    }

    private static XmlElement? FindAssertion(XmlDocument doc)
    {
        var nsManager = new XmlNamespaceManager(doc.NameTable);
        nsManager.AddNamespace("saml", SamlConstants.Namespaces.Assertion);

        return doc.SelectSingleNode("//saml:Assertion", nsManager) as XmlElement;
    }

    private static void ReplaceAssertionWithEncrypted(XmlElement originalAssertion, XmlElement encryptedAssertion)
    {
        var parentNode = originalAssertion.ParentNode;
        if (parentNode is null)
        {
            throw new InvalidOperationException(
                "Cannot replace SAML Assertion because it has no parent node in the XML document.");
        }

        parentNode.ReplaceChild(encryptedAssertion, originalAssertion);
    }

    private static XmlElement EncryptAssertionXml(XmlElement assertion, X509Certificate2 encryptionCertificate, XmlDocument doc)
    {
        var encryptedXml = new EncryptedXml();
        var encryptedData = encryptedXml.Encrypt(assertion, encryptionCertificate);

        var encryptedAssertion = doc.CreateElement("saml", "EncryptedAssertion", SamlConstants.Namespaces.Assertion);
        var encryptedDataElement = doc.ImportNode(encryptedData.GetXml(), true);
        encryptedAssertion.AppendChild(encryptedDataElement);

        return encryptedAssertion;
    }
}

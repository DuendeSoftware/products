// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal class SamlProtocolMessageSigner(
    ISamlSigningService samlSigningService,
    ILogger<SamlProtocolMessageSigner> logger)
{
    internal async Task<string> SignProtocolMessage(XElement messageElement, SamlServiceProvider serviceProvider, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(messageElement);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var certificate = await samlSigningService.GetSigningCertificateAsync(ct);

        logger.SigningSamlProtocolMessage(LogLevel.Debug, serviceProvider.EntityId, messageElement.Name.LocalName);

        try
        {
            var signedXml = XmlSignatureHelper.SignProtocolElement(messageElement, certificate);

            logger.SuccessfullySignedSamlProtocolMessage(LogLevel.Debug, serviceProvider.EntityId, messageElement.Name.LocalName);

            return signedXml;
        }
        catch (Exception ex)
        {
            logger.FailedToSignSamlProtocolMessage(ex, serviceProvider.EntityId, messageElement.Name.LocalName, ex.Message);
            throw;
        }
    }

    internal async Task<string> SignQueryString(string queryString, Ct ct)
    {
        var certificate = await samlSigningService.GetSigningCertificateAsync(ct);
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa == null)
        {
            throw new InvalidOperationException("RSA private key not available for signing.");
        }

        queryString = $"{queryString}&SigAlg={Uri.EscapeDataString("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256")}";

        var bytesToSign = Encoding.UTF8.GetBytes(queryString);

        var signature = rsa.SignData(bytesToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{queryString}&Signature={Uri.EscapeDataString(Convert.ToBase64String(signature))}";
    }
}

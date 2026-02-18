// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Validates signatures on SAML request messages for both HTTP-Redirect and HTTP-POST bindings.
/// </summary>
internal class SamlRequestSignatureValidator<TRequest, TSamlRequest>(TimeProvider timeProvider)
    where TRequest : SamlRequestBase<TSamlRequest>
    where TSamlRequest : ISamlRequest
{
    private static readonly HashSet<string> SupportedAlgorithms =
    [
        "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
        "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512"
    ];

    /// <summary>
    /// Validates signature on HTTP-Redirect binding request.
    /// </summary>
    internal Result<bool, SamlError> ValidateRedirectBindingSignature(
        TRequest request,
        SamlServiceProvider serviceProvider)
    {
        var signature = request.Signature;
        var sigAlg = request.SignatureAlgorithm;

        if (string.IsNullOrEmpty(signature))
        {
            return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Requester, Message = "Missing signature parameter" });
        }

        if (string.IsNullOrEmpty(sigAlg))
        {
            return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Requester, Message = "Missing signature algorithm parameter" });
        }

        if (!SupportedAlgorithms.Contains(sigAlg))
        {
            return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Requester, Message = $"Unsupported signature algorithm: {sigAlg}" });
        }

        // re-create the querystring part that is signed. The spec dictates the exact way this is to be done:
        // SAMLRequest=value&RelayState=value&SigAlg=value
        // The parameters must be URL-encoded
        var queryToVerify = $"SAMLRequest={Uri.EscapeDataString(request.EncodedSamlRequest!)}";

        if (request.RelayState != null)
        {
            queryToVerify += $"&RelayState={Uri.EscapeDataString(request.RelayState)}";
        }

        queryToVerify += $"&SigAlg={Uri.EscapeDataString(sigAlg)}";

        var bytesToVerify = Encoding.UTF8.GetBytes(queryToVerify);
        var signatureBytes = Convert.FromBase64String(signature);

        return ValidateWithCertificates(
            serviceProvider,
            cert => ValidateRedirectSignature(cert, bytesToVerify, signatureBytes, sigAlg));
    }

    private static bool ValidateRedirectSignature(X509Certificate2 cert, byte[] data, byte[] signature, string sigAlg)
    {
        using var rsa = cert.GetRSAPublicKey();
        if (rsa == null)
        {
            return false;
        }

        var hashAlgorithm = sigAlg.Contains("sha512", StringComparison.OrdinalIgnoreCase)
            ? HashAlgorithmName.SHA512
            : HashAlgorithmName.SHA256;

        return rsa.VerifyData(data, signature, hashAlgorithm, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Validates signature on HTTP-POST binding request.
    /// </summary>
    internal Result<bool, SamlError> ValidatePostBindingSignature(
        TRequest request,
        SamlServiceProvider serviceProvider)
    {
        var requestXml = request.RequestXml;

        // In order to use SignedXml, we need to work with XmlDocument, not an XDocument.
        // So we convert the XDocument to string and then parse it securely into an XmlDocument.
        var xmlString = requestXml.ToString(SaveOptions.DisableFormatting);
        XmlDocument doc;

        try
        {
            doc = SecureXmlParser.LoadXmlDocument(xmlString);
        }
        catch (XmlException ex)
        {
            return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Requester, Message = $"Invalid XML: {ex.Message}" });
        }

        // Find signature element
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

        var signatureNode = doc.SelectSingleNode("//ds:Signature", nsmgr);
        if (signatureNode == null)
        {
            return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Requester, Message = "Signature element not found" });
        }

        // Get the request ID that must be signed
        var requestId = doc.DocumentElement?.GetAttribute("ID");
        if (string.IsNullOrEmpty(requestId))
        {
            return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Requester, Message = $"{TSamlRequest.MessageName} missing ID attribute" });
        }

        return ValidateWithCertificates(
            serviceProvider,
            cert => ValidateXmlSignature(cert, doc, signatureNode, requestId));
    }

    private static bool ValidateXmlSignature(X509Certificate2 cert, XmlDocument doc, XmlNode signatureNode, string expectedId)
    {
        var signedXml = new SignedXml(doc);
        signedXml.LoadXml((XmlElement)signatureNode);

        if (!signedXml.CheckSignature(cert, true))
        {
            return false;
        }

        // SECURITY: Verify the signature references the request element
        var reference = signedXml.SignedInfo?.References.Cast<Reference>().FirstOrDefault();
        if (reference == null)
        {
            return false;
        }

        var referencedId = reference.Uri?.TrimStart('#');
        return referencedId == expectedId;
    }

    private Result<bool, SamlError> ValidateWithCertificates(
        SamlServiceProvider serviceProvider,
        Func<X509Certificate2, bool> validateSignature)
    {
        var validCertificates = serviceProvider.SigningCertificates?.Where(cert => ValidateCertificate(cert).Success).ToList();
        if (validCertificates == null || validCertificates.Count == 0)
        {
            return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Responder, Message = "No valid certificates configured for service provider" });
        }

        foreach (var cert in validCertificates)
        {
            if (validateSignature(cert))
            {
                return Result<bool, SamlError>.FromValue(true);
            }
        }

        return Result<bool, SamlError>.FromError(new SamlError { StatusCode = SamlStatusCode.Requester, Message = "Invalid signature" });
    }

    private Result<bool, string> ValidateCertificate(X509Certificate2 certificate)
    {
        var now = timeProvider.GetUtcNow();

        if (certificate.NotBefore > now.UtcDateTime)
        {
            return Result<bool, string>.FromError($"Certificate is not yet valid (NotBefore: {certificate.NotBefore:u})");
        }

        if (certificate.NotAfter < now.UtcDateTime)
        {
            return Result<bool, string>.FromError($"Certificate has expired (NotAfter: {certificate.NotAfter:u})");
        }

        return Result<bool, string>.FromValue(true);
    }
}

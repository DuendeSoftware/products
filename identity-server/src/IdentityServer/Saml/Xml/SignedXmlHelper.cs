// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Duende.IdentityServer.Saml.Xml;

/// <summary>
/// Helpers for SignedXml
/// </summary>
public static class SignedXmlHelper
{
    static SignedXmlHelper()
    {
        // Register ECDSA algorithm URIs with CryptoConfig so that SignedXml can resolve them.
        // .NET's SignedXml does not support ECDSA out of the box — see https://github.com/dotnet/runtime/issues/36103
        // If that issue is resolved, these registrations and the supporting types below can be removed.
        RegisterIfMissing(typeof(EcdsaSha256SignatureDescription), SamlConstants.EcdsaAlgorithms.EcdsaSha256);
        RegisterIfMissing(typeof(EcdsaSha384SignatureDescription), SamlConstants.EcdsaAlgorithms.EcdsaSha384);
        RegisterIfMissing(typeof(EcdsaSha512SignatureDescription), SamlConstants.EcdsaAlgorithms.EcdsaSha512);
    }

    private static void RegisterIfMissing(Type type, string algorithmUri)
    {
        if (CryptoConfig.CreateFromName(algorithmUri) == null)
        {
            CryptoConfig.AddAlgorithm(type, algorithmUri);
        }
    }
    /// <summary>
    /// Adds an enveloped signature to the node.
    /// </summary>
    /// <param name="element">Element to sign</param>
    /// <param name="certificate">Certificate to use to sign</param>
    /// <param name="insertAfter">Insert the signature after this node.</param>
    public static void Sign(
        this XmlElement element,
        X509Certificate2 certificate,
        XmlNode insertAfter)
    {
        ArgumentNullException.ThrowIfNull(insertAfter);

        var signedXml = CreateSignedXml(element, certificate);

        element.InsertAfter(signedXml.GetXml(), insertAfter);
    }

    private static SignedXml CreateSignedXml(XmlElement element, X509Certificate2 certificate)
    {
        // Use OID value for key type detection — FriendlyName is platform-dependent
        AsymmetricAlgorithm signingKey;
        string? ecdsaSignatureMethod = null;

        switch (certificate.PublicKey.Oid.Value)
        {
            case SamlConstants.KeyAlgorithmOids.Rsa:
                signingKey = certificate.GetRSAPrivateKey()
                    ?? throw new InvalidOperationException("RSA private key not available for signing.");
                break;
            case SamlConstants.KeyAlgorithmOids.EcPublicKey:
                var ecdsaKey = certificate.GetECDsaPrivateKey()
                    ?? throw new NotSupportedException("Certificate contains an ECC key that does not support ECDSA signing.");
                signingKey = ecdsaKey;
                // Select signature method to match curve strength per FIPS 186-5 §5.4 (curve-hash pairings)
                // and NIST SP 800-57 Part 1 Rev 5 Table 2 (security strength equivalences):
                // P-256 (128-bit) → SHA-256, P-384 (192-bit) → SHA-384, P-521 (256-bit) → SHA-512.
                // XML signature URIs defined in RFC 6931 §2.3.6.
                ecdsaSignatureMethod = ecdsaKey.KeySize switch
                {
                    256 => SamlConstants.EcdsaAlgorithms.EcdsaSha256,
                    384 => SamlConstants.EcdsaAlgorithms.EcdsaSha384,
                    521 => SamlConstants.EcdsaAlgorithms.EcdsaSha512,
                    _ => throw new NotSupportedException($"ECDSA key size {ecdsaKey.KeySize} is not supported. Supported sizes: 256 (P-256), 384 (P-384), 521 (P-521).")
                };
                break;
            default:
                throw new NotSupportedException($"Certificate key type with OID {certificate.PublicKey.Oid.Value} is not supported for signing.");
        }

        var signedXml = new SignedXml(element.OwnerDocument)
        {
            SigningKey = signingKey
        };

        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NWithCommentsTransformUrl;

        // For ECDSA, explicitly set the signature method — SignedXml does not auto-detect ECDSA
        if (ecdsaSignatureMethod != null)
        {
            signedXml.SignedInfo.SignatureMethod = ecdsaSignatureMethod;
        }

        var id = element.Attributes!["ID"]?.Value;

        var reference = new Reference($"#{id}");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NWithCommentsTransform());

        // Match digest strength to curve strength per FIPS 186-5 §5.4 / NIST SP 800-57 Part 1 Rev 5 Table 2
        if (ecdsaSignatureMethod == SamlConstants.EcdsaAlgorithms.EcdsaSha384)
        {
            reference.DigestMethod = SignedXml.XmlDsigSHA384Url;
        }
        else if (ecdsaSignatureMethod == SamlConstants.EcdsaAlgorithms.EcdsaSha512)
        {
            reference.DigestMethod = SignedXml.XmlDsigSHA512Url;
        }

        signedXml.AddReference(reference);

        signedXml.KeyInfo.AddClause(new KeyInfoX509Data(certificate));

        signedXml.ComputeSignature();
        return signedXml;
    }

    /// <summary>
    /// Signed Xml version that is strict that the ID attribute must be exactly ID and
    /// not contains any weird fallback behaviour.
    /// </summary>
    internal class SignedXmlWithStrictIdResolution : SignedXml
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="xmlDocument">Xml document</param>
        internal SignedXmlWithStrictIdResolution(XmlDocument xmlDocument)
            : base(xmlDocument)
        { }

        /// <summary>
        /// Get Id Element, being strict
        /// </summary>
        /// <param name="document">Xml Document</param>
        /// <param name="idValue">Id value to find</param>
        /// <returns>XmlElement</returns>
        /// <exception cref="CryptographicException">If not exactly one match</exception>
        public override XmlElement GetIdElement(XmlDocument? document, string idValue)
        {
            ArgumentNullException.ThrowIfNull(document);

            XmlConvert.VerifyNCName(idValue);

            var possibleNodes = document.SelectNodes($"//*[@ID=\"{idValue}\" or @Id=\"{idValue}\" or @id=\"{idValue}\"]")!;

            if (possibleNodes.Count != 1)
            {
                throw new CryptographicException("Reference target should resolve to exactly one node");
            }

            var element = (XmlElement)possibleNodes[0]!;

            // If we don't find the ID attribute it means it matched Id or id, which is not allowed.
            _ = element.GetAttributeNode("ID")
                ?? throw new CryptographicException("Reference target ID attribute must be named ID with uppercase letters");

            return element;
        }
    }

    /// <summary>
    /// Verifies a found Xml signature.
    /// </summary>
    /// <param name="signatureElement">The signature element to verify.</param>
    /// <param name="keys">The signing keys that can be used to verify.</param>
    /// <param name="allowedAlgorithms">Allowed algorithms. Values must be full algorithm identifier URIs
    /// (e.g. <c>http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256</c>), matched by exact equality against
    /// both the digest method and signature method in the signed XML.</param>
    /// <returns>Tuple with possibly error message, and the signing key that worked.</returns>
    internal static (string? Error, SigningKey? WorkingKey) VerifySignature(
        this XmlElement signatureElement,
        IEnumerable<SigningKey> keys,
        IEnumerable<string> allowedAlgorithms)
    {
        try
        {
            return VerifySignatureCore(signatureElement, keys, allowedAlgorithms);
        }
        catch (CryptographicException e)
        {
            return (e.Message, null);
        }
    }

    private static (string? Error, SigningKey? WorkingKey) VerifySignatureCore(
        XmlElement signatureElement,
        IEnumerable<SigningKey> keys,
        IEnumerable<string> allowedAlgorithms)
    {
        var signedXml = new SignedXmlWithStrictIdResolution(signatureElement.OwnerDocument);

        signedXml.LoadXml(signatureElement);

        string? error = null;
        SigningKey? workingKey = null;

        if (signedXml.SignedInfo!.References.Count != 1)
        {
            error += "The Signature should contain exactly one reference. ";
        }
        else
        {
            foreach (var key in keys)
            {
                if (key.Certificate == null)
                {
                    throw new InvalidOperationException("Signing key certificate cannot be null");
                }

                if (signedXml.CheckSignature(key.Certificate, true))
                {
                    workingKey = key;
                    break;
                }
            }

            if (workingKey == null)
            {
                if (signedXml.CheckSignatureReturningKey(out _))
                {
                    error += "Signature validated with the contained key, but that is not configured as a trusted key. ";
                }
                else
                {
                    error += "Signature didn't verify for any of the the specified keys. ";
                }
            }

            var reference = (Reference)signedXml.SignedInfo.References[0]!;

            if (reference.Uri!.Length == 0)
            {
                error += "Empty reference URI (implying the whole document is signed) is not allowed in Saml2. ";
            }
            else
            {
                var id = reference.Uri![1..]; // Drop off the #

                var signedElement = signedXml.GetIdElement(signatureElement.OwnerDocument, id);

                if (signedElement == null || signedElement != signatureElement.ParentNode)
                {
                    error += "Incorrect reference on Xml Signature, the reference must be to the parent element of the signature. ";
                }
            }

            foreach (Transform transform in reference.TransformChain)
            {
                switch (transform.Algorithm)
                {
                    case SignedXml.XmlDsigEnvelopedSignatureTransformUrl:
                    case SignedXml.XmlDsigExcC14NTransformUrl:
                    case SignedXml.XmlDsigExcC14NWithCommentsTransformUrl:
                        break;
                    default:
                        error += $"Transform {transform.Algorithm} is not allowed in SAML2. ";
                        break;
                }
            }

            if (!allowedAlgorithms.Contains(reference.DigestMethod))
            {
                var allowed = string.Join(", ", allowedAlgorithms);
                error += $"Digest algorithm {reference.DigestMethod} does not match configured [{allowed}]. ";
            }
        }

        if (!allowedAlgorithms.Contains(signedXml.SignatureMethod))
        {
            var allowed = string.Join(", ", allowedAlgorithms);
            error += $"Signature algorithm {signedXml.SignatureMethod} does not match configured [{allowed}]. ";
        }

        return (error?.TrimEnd(), workingKey);
    }
}

/// <summary>
/// Base <see cref="SignatureDescription"/> for ECDSA algorithms.
/// Required to register ECDSA algorithm URIs with <see cref="CryptoConfig"/> so that
/// <see cref="SignedXml"/> can resolve and use them for signing and verification.
/// Must be <c>public</c> because <see cref="CryptoConfig.AddAlgorithm"/> requires types
/// accessible from outside their assembly.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public abstract class EcdsaSignatureDescriptionBase : SignatureDescription
{
    protected EcdsaSignatureDescriptionBase() =>
        KeyAlgorithm = typeof(ECDsa).AssemblyQualifiedName;

    /// <inheritdoc/>
    public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
    {
        if (key is not ECDsa ecdsa)
        {
            throw new InvalidOperationException("ECDSA signature formatter requires an ECDsa key.");
        }

        return new EcdsaSignatureFormatter(ecdsa);
    }

    /// <inheritdoc/>
    public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
    {
        if (key is not ECDsa ecdsa)
        {
            throw new InvalidOperationException("ECDSA signature deformatter requires an ECDsa key.");
        }

        return new EcdsaSignatureDeformatter(ecdsa);
    }
}

/// <summary>ECDSA-SHA256 signature description for use with <see cref="SignedXml"/>.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class EcdsaSha256SignatureDescription : EcdsaSignatureDescriptionBase
{
    /// <summary>Initializes a new instance.</summary>
    public EcdsaSha256SignatureDescription() =>
        DigestAlgorithm = typeof(SHA256).AssemblyQualifiedName;

    /// <inheritdoc/>
    public override HashAlgorithm CreateDigest() => SHA256.Create();
}

/// <summary>ECDSA-SHA384 signature description for use with <see cref="SignedXml"/>.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class EcdsaSha384SignatureDescription : EcdsaSignatureDescriptionBase
{
    /// <summary>Initializes a new instance.</summary>
    public EcdsaSha384SignatureDescription() =>
        DigestAlgorithm = typeof(SHA384).AssemblyQualifiedName;

    /// <inheritdoc/>
    public override HashAlgorithm CreateDigest() => SHA384.Create();
}

/// <summary>ECDSA-SHA512 signature description for use with <see cref="SignedXml"/>.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class EcdsaSha512SignatureDescription : EcdsaSignatureDescriptionBase
{
    /// <summary>Initializes a new instance.</summary>
    public EcdsaSha512SignatureDescription() =>
        DigestAlgorithm = typeof(SHA512).AssemblyQualifiedName;

    /// <inheritdoc/>
    public override HashAlgorithm CreateDigest() => SHA512.Create();
}

/// <summary>
/// ECDSA signature formatter for use with <see cref="SignedXml"/>.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class EcdsaSignatureFormatter : AsymmetricSignatureFormatter
{
    private ECDsa _key;

    /// <summary>Initializes a new instance with the given key.</summary>
    public EcdsaSignatureFormatter(ECDsa key) => _key = key;

    /// <inheritdoc/>
    public override void SetKey(AsymmetricAlgorithm key) =>
        _key = key as ECDsa ?? throw new InvalidOperationException("Key must be ECDsa.");

    /// <inheritdoc/>
    public override void SetHashAlgorithm(string strName)
    {
        // Intentional no-op — hash algorithm is determined by the SignatureDescription's digest.
    }

    /// <inheritdoc/>
    public override byte[] CreateSignature(byte[] rgbHash) => _key.SignHash(rgbHash);
}

/// <summary>
/// ECDSA signature deformatter for use with <see cref="SignedXml"/>.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class EcdsaSignatureDeformatter : AsymmetricSignatureDeformatter
{
    private ECDsa _key;

    /// <summary>Initializes a new instance with the given key.</summary>
    public EcdsaSignatureDeformatter(ECDsa key) => _key = key;

    /// <inheritdoc/>
    public override void SetKey(AsymmetricAlgorithm key) =>
        _key = key as ECDsa ?? throw new InvalidOperationException("Key must be ECDsa.");

    /// <inheritdoc/>
    public override void SetHashAlgorithm(string strName)
    {
        // Intentional no-op — hash algorithm is determined by the SignatureDescription's digest.
    }

    /// <inheritdoc/>
    public override bool VerifySignature(byte[] rgbHash, byte[] rgbSignature) =>
        _key.VerifyHash(rgbHash, rgbSignature);
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    internal class X509AsymmetricSecurityKey : AsymmetricSecurityKey
    {
        X509Certificate2 certificate;

        public X509AsymmetricSecurityKey(X509Certificate2 cert)
        {
            if (cert == null)
            {
                throw new ArgumentNullException(nameof(cert));
            }

            certificate = cert;
        }

        public override AsymmetricAlgorithm GetAsymmetricAlgorithm(string algorithm, bool privateKey)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            if (privateKey && !certificate.HasPrivateKey)
            {
                throw new NotSupportedException("The certificate doesn't have a private key");
            }

            AsymmetricAlgorithm aa = privateKey ? certificate.PrivateKey : certificate.PublicKey.Key;

            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:
                    if (aa is DSA)
                        return aa;
                    throw new NotSupportedException("The certificate does not support DSA signing");
                case SignedXml.XmlDsigRSASHA1Url:
                case SignedXml.XmlDsigSHA1Url:
                case EncryptedXml.XmlEncRSA15Url:
                case EncryptedXml.XmlEncRSAOAEPUrl:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    if (aa is RSA)
                        return aa;
                    throw new NotSupportedException("The certificate does not support RSA signing");
            }

            throw new NotSupportedException($"The certificate does not support the algorithm '{algorithm}'");
        }

        public override HashAlgorithm GetHashAlgorithmForSignature(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:
                case SignedXml.XmlDsigRSASHA1Url:
                    return new SHA1Managed();
                case SecurityAlgorithms.RsaSha256Signature:
                    return new SHA256Managed();
                case SecurityAlgorithms.RsaSha384Signature:
                    return new SHA384Managed();
                case SecurityAlgorithms.RsaSha512Signature:
                    return new SHA512Managed();
            }
            throw new NotSupportedException($"The hash algorithm '{algorithm}' is not supported");
        }

        public override AsymmetricSignatureDeformatter GetSignatureDeformatter(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:
                    {
                        DSA dsa = certificate.PublicKey.Key as DSA;
                        if (dsa == null)
                        {
                            throw new NotSupportedException("The certificate does not contain a DSA key");
                        }
                        return new DSASignatureDeformatter(dsa);
                    }
                case SignedXml.XmlDsigRSASHA1Url:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    {
                        RSA rsa = certificate.PublicKey.Key as RSA;
                        if (rsa == null)
                        {
                            throw new NotSupportedException("The certificate does not contain an RSA key");
                        }
                        return new RSAPKCS1SignatureDeformatter(rsa);
                    }
            }
            throw new NotSupportedException($"The signature algorithm '{algorithm}' is not supported");
        }

        public override AsymmetricSignatureFormatter GetSignatureFormatter(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:
                    {
                        DSA dsa = certificate.PrivateKey as DSA;
                        if (dsa == null)
                        {
                            throw new NotSupportedException("The certificate does not contain a DSA key");
                        }
                        return new DSASignatureFormatter(dsa);
                    }
                case SignedXml.XmlDsigRSASHA1Url:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    {
                        RSA rsa = certificate.PrivateKey as RSA;
                        if (rsa == null)
                        {
                            throw new NotSupportedException("The certificate does not contain an RSA key");
                        }
                        return new RSAPKCS1SignatureFormatter(rsa);
                    }
            }
            throw new NotSupportedException($"The signature algorithm '{algorithm}' is not supported");
        }

        public override int KeySize => certificate.PublicKey.Key.KeySize;

        public override byte[] DecryptKey(string algorithm, byte[] keyData)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            if (keyData == null)
            {
                throw new ArgumentNullException(nameof(keyData));
            }

            if (!HasPrivateKey())
            {
                throw new NotSupportedException("The certificate does not have a private key");
            }
            if (certificate.PrivateKey.KeyExchangeAlgorithm == null)
            {
                throw new NotSupportedException("The certificate does not have a key exchange algorithm");
            }

            bool useOAEP;
            switch (algorithm)
            {
                case EncryptedXml.XmlEncRSA15Url:
                    useOAEP = false;
                    break;
                case EncryptedXml.XmlEncRSAOAEPUrl:
                    useOAEP = true;
                    break;
                default:
                    throw new NotSupportedException($"The encryption algorithm {algorithm} is not supported");
            }
            return EncryptedXml.DecryptKey(keyData, certificate.PrivateKey as RSA, useOAEP);
        }

        public override byte[] EncryptKey(string algorithm, byte[] keyData)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            if (keyData == null)
            {
                throw new ArgumentNullException(nameof(keyData));
            }

            bool useOAEP;
            switch (algorithm)
            {
                case EncryptedXml.XmlEncRSA15Url:
                    useOAEP = false;
                    break;
                case EncryptedXml.XmlEncRSAOAEPUrl:
                    useOAEP = true;
                    break;
                default:
                    throw new NotSupportedException($"The encryption algorithm {algorithm} is not supported");
            }
            return EncryptedXml.EncryptKey(keyData, certificate.PublicKey.Key as RSA, useOAEP);
        }

        public override bool IsAsymmetricAlgorithm(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SecurityAlgorithms.DsaSha1Signature:
                case SecurityAlgorithms.RsaV15KeyWrap:
                case SecurityAlgorithms.RsaOaepKeyWrap:
                case SecurityAlgorithms.RsaSha1Signature:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    return true;
            }
            return false;
        }

        public override bool IsSupportedAlgorithm(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SecurityAlgorithms.RsaV15KeyWrap:
                case SecurityAlgorithms.RsaOaepKeyWrap:
                case SecurityAlgorithms.RsaSha1Signature:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    return true;
            }
            return false;
        }

        public override bool IsSymmetricAlgorithm(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SecurityAlgorithms.HmacSha1Signature:
                case SecurityAlgorithms.Psha1KeyDerivation:
                case SecurityAlgorithms.Aes128Encryption:
                case SecurityAlgorithms.Aes128KeyWrap:
                case SecurityAlgorithms.Aes192Encryption:
                case SecurityAlgorithms.Aes192KeyWrap:
                case SecurityAlgorithms.Aes256Encryption:
                case SecurityAlgorithms.Aes256KeyWrap:
                case SecurityAlgorithms.TripleDesEncryption:
                case SecurityAlgorithms.TripleDesKeyWrap:
                case SecurityAlgorithms.DesEncryption:
                    return true;
            }
            return false;
        }

        public override bool HasPrivateKey()
        {
            return certificate.HasPrivateKey;
        }
    }
}

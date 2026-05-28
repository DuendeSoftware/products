// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Microsoft.IdentityModel.Tokens;
using EncryptingCredentials = Microsoft.IdentityModel.Tokens.EncryptingCredentials;
using SecurityAlgorithms = Microsoft.IdentityModel.Tokens.SecurityAlgorithms;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    internal static class CryptographyExtensions
    {
        internal static void Encrypt(this XmlElement elementToEncrypt, EncryptingCredentials encryptingCredentials)
        {
            if (elementToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(elementToEncrypt));
            }
            if (encryptingCredentials == null)
            {
                throw new ArgumentNullException(nameof(encryptingCredentials));
            }

            string enc;
            int keySize;
            switch (encryptingCredentials.Enc)
            {
                case SecurityAlgorithms.Aes128CbcHmacSha256:
                    enc = EncryptedXml.XmlEncAES128Url;
                    keySize = 128;
                    break;
                case SecurityAlgorithms.Aes192CbcHmacSha384:
                    enc = EncryptedXml.XmlEncAES192Url;
                    keySize = 192;
                    break;
                case SecurityAlgorithms.Aes256CbcHmacSha512:
                    enc = EncryptedXml.XmlEncAES256Url;
                    keySize = 256;
                    break;
                default:
                    throw new CryptographicException(
                        $"Unsupported cryptographic algorithm {encryptingCredentials.Enc}");
            }

            var encryptedData = new EncryptedData
            {
                Type = EncryptedXml.XmlEncElementUrl,
                EncryptionMethod = new EncryptionMethod(enc)
            };

            string alg;
            switch (encryptingCredentials.Alg)
            {
                case SecurityAlgorithms.RsaOAEP:
                    alg = EncryptedXml.XmlEncRSAOAEPUrl;
                    break;
                case SecurityAlgorithms.RsaPKCS1:
                    alg = EncryptedXml.XmlEncRSA15Url;
                    break;
                default:
                    throw new CryptographicException(
                        $"Unsupported cryptographic algorithm {encryptingCredentials.Alg}");
            }
            var encryptedKey = new EncryptedKey
            {
                EncryptionMethod = new EncryptionMethod(alg),
            };

            var encryptedXml = new EncryptedXml();
            byte[] encryptedElement;
            using (var symmetricAlgorithm = new RijndaelManaged())
            {
                X509SecurityKey x509SecurityKey = encryptingCredentials.Key as X509SecurityKey;
                if (x509SecurityKey == null)
                {
                    throw new CryptographicException(
                        "The encrypting credentials have an unknown key of type {encryptingCredentials.Key.GetType()}");
                }

                symmetricAlgorithm.KeySize = keySize;
                encryptedKey.CipherData = new CipherData(EncryptedXml.EncryptKey(symmetricAlgorithm.Key,
                    (RSA)x509SecurityKey.PublicKey, alg == EncryptedXml.XmlEncRSAOAEPUrl));
                encryptedElement = encryptedXml.EncryptData(elementToEncrypt, symmetricAlgorithm, false);
            }
            encryptedData.CipherData.CipherValue = encryptedElement;

            encryptedData.KeyInfo = new KeyInfo();
            encryptedData.KeyInfo.AddClause(new KeyInfoEncryptedKey(encryptedKey));
            EncryptedXml.ReplaceElement(elementToEncrypt, encryptedData, false);
        }

        internal static void Encrypt(this XmlElement elementToEncrypt, bool useOaep, X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            var encryptedData = new EncryptedData
            {
                Type = EncryptedXml.XmlEncElementUrl,
                EncryptionMethod = new EncryptionMethod(EncryptedXml.XmlEncAES256Url)
            };

            var algorithm = useOaep ? EncryptedXml.XmlEncRSAOAEPUrl : EncryptedXml.XmlEncRSA15Url;
            var encryptedKey = new EncryptedKey
            {
                EncryptionMethod = new EncryptionMethod(algorithm),
            };

            var encryptedXml = new EncryptedXml();
            byte[] encryptedElement;
            using (var symmetricAlgorithm =
                CryptoConfig.AllowOnlyFipsAlgorithms
                ? (SymmetricAlgorithm)new AesCryptoServiceProvider()
                : (SymmetricAlgorithm)new RijndaelManaged())
            {
                symmetricAlgorithm.KeySize = 256;
                encryptedKey.CipherData = new CipherData(EncryptedXml.EncryptKey(symmetricAlgorithm.Key, (RSA)certificate.PublicKey.Key, useOaep));
                encryptedElement = encryptedXml.EncryptData(elementToEncrypt, symmetricAlgorithm, false);
            }
            encryptedData.CipherData.CipherValue = encryptedElement;

            encryptedData.KeyInfo = new KeyInfo();
            encryptedData.KeyInfo.AddClause(new KeyInfoEncryptedKey(encryptedKey));
            EncryptedXml.ReplaceElement(elementToEncrypt, encryptedData, false);
        }

        internal static IEnumerable<XmlElement> Decrypt(this IEnumerable<XmlElement> elements, AsymmetricAlgorithm key)
        {
            foreach (var element in elements)
            {
                yield return element.Decrypt(key);
            }
        }

        internal static XmlElement Decrypt(this XmlElement element, AsymmetricAlgorithm key)
        {
            var xmlDoc = XmlHelpers.XmlDocumentFromString(element.OuterXml);

            var exml = new RSAEncryptedXml(xmlDoc, (RSA)key);

            exml.DecryptDocument();

            return xmlDoc.DocumentElement;
        }

        internal static AsymmetricAlgorithm GetSha256EnabledAsymmetricAlgorithm(this X509Certificate2 x509Certificate2)
        {
            var ecDsa = x509Certificate2.GetECDsaPrivateKey();

            if (ecDsa != null)
            {
                return ecDsa;
            }

            var rsa = x509Certificate2.GetRSAPrivateKey();

            if (rsa != null)
            {
                return rsa.GetSha256EnabledRSACryptoServiceProvider();
            }

            throw new NotImplementedException();
        }

        internal static RSA GetSha256EnabledRSACryptoServiceProvider(this RSA rsa)
        {
            return rsa;
        }

        public static object CreateAlgorithmFromName(string name, params object[] args)
        {
            var result = CryptoConfig.CreateFromName(name);
            if (result != null)
            {
                return result;
            }

            // .NET Core+ does not register XML signature algorithm URIs as SignatureDescription
            // in CryptoConfig. Map them manually.
            return name switch
            {
                "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256" => new RsaSha256SignatureDescription(),
                "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384" => new RsaSha384SignatureDescription(),
                "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512" => new RsaSha512SignatureDescription(),
                "http://www.w3.org/2000/09/xmldsig#rsa-sha1" => new RsaSha1SignatureDescription(),
                _ => throw new CryptographicException($"Unknown crypto algorithm '{name}'")
            };
        }
    }

    internal abstract class RsaSignatureDescription : SignatureDescription
    {
        protected RsaSignatureDescription()
        {
            KeyAlgorithm = typeof(RSA).AssemblyQualifiedName;
            FormatterAlgorithm = typeof(RSAPKCS1SignatureFormatter).AssemblyQualifiedName;
            DeformatterAlgorithm = typeof(RSAPKCS1SignatureDeformatter).AssemblyQualifiedName;
        }

        protected abstract HashAlgorithmName HashName { get; }

        public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
        {
            var deformatter = new RSAPKCS1SignatureDeformatter(key);
            deformatter.SetHashAlgorithm(HashName.Name!);
            return deformatter;
        }

        public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
        {
            var formatter = new RSAPKCS1SignatureFormatter(key);
            formatter.SetHashAlgorithm(HashName.Name!);
            return formatter;
        }
    }

    internal sealed class RsaSha1SignatureDescription : RsaSignatureDescription
    {
        protected override HashAlgorithmName HashName => HashAlgorithmName.SHA1;
        public override HashAlgorithm CreateDigest() => SHA1.Create();
    }

    internal sealed class RsaSha256SignatureDescription : RsaSignatureDescription
    {
        protected override HashAlgorithmName HashName => HashAlgorithmName.SHA256;
        public override HashAlgorithm CreateDigest() => SHA256.Create();
    }

    internal sealed class RsaSha384SignatureDescription : RsaSignatureDescription
    {
        protected override HashAlgorithmName HashName => HashAlgorithmName.SHA384;
        public override HashAlgorithm CreateDigest() => SHA384.Create();
    }

    internal sealed class RsaSha512SignatureDescription : RsaSignatureDescription
    {
        protected override HashAlgorithmName HashName => HashAlgorithmName.SHA512;
        public override HashAlgorithm CreateDigest() => SHA512.Create();
    }
}

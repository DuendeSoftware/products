// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    internal class RSAEncryptedXml : EncryptedXml
    {
        private readonly RSA privateKey;
        public const string XmlEncRSAOAEPGenericUrl = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

        public RSAEncryptedXml(XmlDocument document, RSA rsaKey)
            : base(document)
        {
            privateKey = rsaKey;
        }

        // Try to decrypt the EncryptedKey by the given key
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "CipherReference"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "CipherData")]
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
        public override byte[] DecryptEncryptedKey(EncryptedKey encryptedKey)
        {
            if (encryptedKey == null)
            {
                throw new ArgumentNullException(nameof(encryptedKey));
            }

            if (encryptedKey.CipherData.CipherValue == null)
            {
                throw new NotImplementedException("Unable to decode CipherData of type \"CipherReference\".");
            }

            // use the key info
            if (privateKey == null)
            {
                return base.DecryptEncryptedKey(encryptedKey);
            }

            if (encryptedKey.EncryptionMethod != null &&
                  encryptedKey.EncryptionMethod.KeyAlgorithm == XmlEncRSAOAEPGenericUrl)
            {
                return GenericOaepDecryptKey(encryptedKey.CipherData.CipherValue, privateKey, encryptedKey.EncryptionMethod);
            }

            var useOaep = (encryptedKey.EncryptionMethod != null &&
                         encryptedKey.EncryptionMethod.KeyAlgorithm == XmlEncRSAOAEPUrl);

            return DecryptKey(encryptedKey.CipherData.CipherValue, privateKey, useOaep);
        }

        private byte[] GenericOaepDecryptKey(byte[] cipherValue, RSA privateKey, EncryptionMethod encryptionMethod)
        {
            var xml = encryptionMethod.GetXml();
            var hashAlgorithm = xml["DigestMethod", SignedXml.XmlDsigNamespaceUrl].Attributes["Algorithm"].Value;

            RSAEncryptionPadding padding;

            switch (hashAlgorithm)
            {
                case SignedXml.XmlDsigRSASHA1Url:
                    padding = RSAEncryptionPadding.OaepSHA1;
                    break;
                case "http://www.w3.org/2001/04/xmlenc#sha256":
                    padding = RSAEncryptionPadding.OaepSHA256;
                    break;
                case "http://www.w3.org/2001/04/xmlenc#sha384":
                    padding = RSAEncryptionPadding.OaepSHA384;
                    break;
                case "http://www.w3.org/2001/04/xmlenc#sha512":
                    padding = RSAEncryptionPadding.OaepSHA512;
                    break;
                default:
                    throw new NotImplementedException($"Unknown hash algorithm for OAEP {hashAlgorithm}");
            }
            ;

            return privateKey.Decrypt(cipherValue, padding);
        }

        /// <summary>
        /// AES-GCM Nonce size defined in https://www.w3.org/TR/xmlenc-core1/#sec-AES-GCM
        /// </summary>
        internal const int AesGcm128NonceSizeInBits = 96;

        public override byte[] GetDecryptionIV(EncryptedData encryptedData, string symmetricAlgorithmUri)
        {
            //adapted from https://github.com/dotnet/runtime/blob/a5192d4963531579166d7f43df2a1ed44a96900f/src/libraries/System.Security.Cryptography.Xml/src/System/Security/Cryptography/Xml/EncryptedXml.cs#L267
            if (symmetricAlgorithmUri == null && encryptedData.EncryptionMethod != null)
            {
                symmetricAlgorithmUri = encryptedData.EncryptionMethod.KeyAlgorithm;
            }

            switch (symmetricAlgorithmUri)
            {
                case AesGcmAlgorithm.AesGcm128Identifier:
                case AesGcmAlgorithm.AesGcm256Identifier:
                    const int initBytesSize = AesGcm128NonceSizeInBits / 8;
                    var iv = new byte[initBytesSize];
                    var cipherValue = encryptedData.CipherData.CipherValue;
                    Buffer.BlockCopy(cipherValue, 0, iv, 0, iv.Length);
                    return iv;
                default:
                    return base.GetDecryptionIV(encryptedData, symmetricAlgorithmUri);
            }
        }

        private static bool IsAes(string uri) =>
            uri.StartsWith("http://www.w3.org/2001/04/xmlenc#aes")
            || uri.StartsWith("http://www.w3.org/2001/04/xmlenc#kw-aes");

        public override SymmetricAlgorithm GetDecryptionKey(EncryptedData encryptedData, string symmetricAlgorithmUri)
        {
            if (IsAes(encryptedData?.EncryptionMethod?.KeyAlgorithm))
            {
                EnsureAesRegistered();

                if (CryptoConfig.AllowOnlyFipsAlgorithms)
                {
                    var encryptedKey = encryptedData.KeyInfo.OfType<KeyInfoEncryptedKey>().FirstOrDefault().EncryptedKey;

                    if (encryptedKey != null)
                    {
                        var key = DecryptEncryptedKey(encryptedKey);

                        if (key != null)
                        {
                            return new AesCryptoServiceProvider()
                            {
                                Key = key,
                            };
                        }
                    }

                }
            }

            return base.GetDecryptionKey(encryptedData, symmetricAlgorithmUri);
        }
        private static bool aesRegistered = false;

        private void EnsureAesRegistered()
        {
            if (!aesRegistered)
            {
                var aes128 = CryptoConfig.CreateFromName(AesGcmAlgorithm.AesGcm128Identifier);
                if (aes128 == null)
                {
                    CryptoConfig.AddAlgorithm(typeof(AesGcmAlgorithm128), AesGcmAlgorithm.AesGcm128Identifier);
                }

                var aes256 = CryptoConfig.CreateFromName(AesGcmAlgorithm.AesGcm256Identifier);
                if (aes256 == null)
                {
                    CryptoConfig.AddAlgorithm(typeof(AesGcmAlgorithm256), AesGcmAlgorithm.AesGcm256Identifier);
                }

                aesRegistered = true;
            }
        }
    }
}

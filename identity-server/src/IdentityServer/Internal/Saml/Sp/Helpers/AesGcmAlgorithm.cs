// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;


namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    /// <summary>
    /// SymmetricAlgorithm decrypting implementation for http://www.w3.org/2009/xmlenc11#aes128-gcm.
    /// This is class is not a general implementation and can only do decryption.
    /// </summary>
    internal class AesGcmAlgorithm
        : SymmetricAlgorithm
    {
        public const string AesGcm128Identifier = "http://www.w3.org/2009/xmlenc11#aes128-gcm";
        public const string AesGcm256Identifier = "http://www.w3.org/2009/xmlenc11#aes256-gcm";

        // "For the purposes of this specification, AES-GCM shall be used with a 96 bit Initialization Vector (IV) and a 128 bit Authentication Tag (T)."
        // Source: https://www.w3.org/TR/xmlenc-core1/#sec-AES-GCM
        public const int NonceSizeInBits = 96;

        private const int AuthenticationTagSizeInBits = 128;

        protected AesGcmAlgorithm(int keySize)
        {
            LegalKeySizesValue = new[] { new KeySizes(keySize, keySize, 0) };

            //iv setter checks that iv is the size of a block. Not sure if there should be other block sizes
            LegalBlockSizesValue = new[] { new KeySizes(NonceSizeInBits, NonceSizeInBits, 0) };
            BlockSizeValue = NonceSizeInBits;
            //dummy iv value since it is accessed first in EncryptedXml.DecryptData
            IV = new byte[NonceSizeInBits / 8];
        }

        public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV)
        {
            return new AesGcmDecryptor(rgbKey, rgbIV, AuthenticationTagSizeInBits);
        }

        public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
        {
            throw new NotImplementedException();
        }

        public override void GenerateIV()
        {
            throw new NotImplementedException();
        }

        public override void GenerateKey()
        {
            throw new NotImplementedException();
        }
    }

    internal class AesGcmAlgorithm128 : AesGcmAlgorithm
    {
        public AesGcmAlgorithm128() : base(128) { }
    }
    internal class AesGcmAlgorithm256 : AesGcmAlgorithm
    {
        public AesGcmAlgorithm256() : base(256) { }
    }
}

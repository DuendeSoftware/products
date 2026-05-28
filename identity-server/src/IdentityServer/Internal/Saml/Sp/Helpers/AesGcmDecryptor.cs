// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{

    internal class AesGcmDecryptor : ICryptoTransform
    {
        private readonly byte[] key;
        private readonly byte[] nonce;
        private readonly int authenticationTagSizeInBits;

        public AesGcmDecryptor(byte[] key, byte[] nonce, int authenticationTagSizeInBits)
        {
            this.key = key;
            this.nonce = nonce;
            this.authenticationTagSizeInBits = authenticationTagSizeInBits;
        }

        public bool CanReuseTransform => throw new NotImplementedException();

        public bool CanTransformMultipleBlocks => throw new NotImplementedException();

        public int InputBlockSize => throw new NotImplementedException();

        public int OutputBlockSize => throw new NotImplementedException();

        public void Dispose() { }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            throw new NotImplementedException();
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            //inspired by https://stackoverflow.com/a/60891115

            var tagSize = authenticationTagSizeInBits / 8;
            var cipherSize = inputCount - tagSize;

            // "The cipher text contains the IV first, followed by the encrypted octets and finally the Authentication tag."
            // https://www.w3.org/TR/xmlenc-core1/#sec-AES-GCM
            var encryptedData = inputBuffer.Skip(inputOffset).Take(inputCount).ToArray();
            var tag = encryptedData.Skip(encryptedData.Length - tagSize)
                .Take(tagSize).ToArray();

            var cipherBytes = encryptedData.Take(cipherSize).ToArray();

            var plainBytes = cipherSize < 1024
              ? new byte[cipherSize]
              : new byte[cipherSize];

            using (var aes = new AesGcm(key))
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return plainBytes.ToArray();
        }
    }
}

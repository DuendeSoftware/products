// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    internal abstract class SecurityKey
    {
        public abstract int KeySize { get; }
        public abstract byte[] DecryptKey(string algorithm, byte[] keyData);
        public abstract byte[] EncryptKey(string algorithm, byte[] keyData);
        public abstract bool IsAsymmetricAlgorithm(string algorithm);
        public abstract bool IsSupportedAlgorithm(string algorithm);
        public abstract bool IsSymmetricAlgorithm(string algorithm);
    }
}

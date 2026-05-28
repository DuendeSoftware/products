// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;

namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    internal abstract class AsymmetricSecurityKey : SecurityKey
    {
        public abstract AsymmetricAlgorithm GetAsymmetricAlgorithm(string algorithm, bool privateKey);
        public abstract HashAlgorithm GetHashAlgorithmForSignature(string algorithm);
        public abstract AsymmetricSignatureDeformatter GetSignatureDeformatter(string algorithm);
        public abstract AsymmetricSignatureFormatter GetSignatureFormatter(string algorithm);
        public abstract bool HasPrivateKey();
    }
}

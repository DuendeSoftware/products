// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;

namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    internal class RsaSecurityKey : AsymmetricSecurityKey
    {
        RSA rsa;

        public RsaSecurityKey(RSA rsa)
        {
            this.rsa = rsa;
        }

        public override AsymmetricAlgorithm GetAsymmetricAlgorithm(string algorithm, bool privateKey)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }

            switch (algorithm)
            {
                case SignedXml.XmlDsigRSASHA1Url:
                case SignedXml.XmlDsigSHA1Url:
                case EncryptedXml.XmlEncRSA15Url:
                case EncryptedXml.XmlEncRSAOAEPUrl:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    return rsa;
            }

            throw new NotSupportedException($"RsaSecurityKey does not support the algorithm '{algorithm}'");
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
                case SignedXml.XmlDsigRSASHA1Url:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    return new RSAPKCS1SignatureDeformatter(rsa);
            }
            throw new NotSupportedException($"RsaSecurityKey does not support the signature algorithm '{algorithm}'");
        }

        public override AsymmetricSignatureFormatter GetSignatureFormatter(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SignedXml.XmlDsigRSASHA1Url:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaSha384Signature:
                case SecurityAlgorithms.RsaSha512Signature:
                    return new RSAPKCS1SignatureFormatter(rsa);
            }
            throw new NotSupportedException($"RsaSecurityKey does not support the signature algorithm '{algorithm}'");
        }

        public override int KeySize => rsa.KeySize;

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

            if (rsa.KeyExchangeAlgorithm == null)
            {
                throw new NotSupportedException("The RSA key does not have a key exchange algorithm");
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
            return EncryptedXml.DecryptKey(keyData, rsa, useOAEP);
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
            return EncryptedXml.EncryptKey(keyData, rsa as RSA, useOAEP);
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
            return true;
        }
    }

    internal class RsaKeyIdentifierClause : SecurityKeyIdentifierClause
    {
        readonly RSAParameters parameters;
        RsaSecurityKey key;
        RSA rsa;

        public RSA Rsa
        {
            get
            {
                if (rsa == null)
                {
                    rsa = RSA.Create();
                    rsa.ImportParameters(parameters);
                }
                return rsa;
            }
        }


        public RsaKeyIdentifierClause(RSAParameters parameters) :
            base(null)
        {
            this.parameters = parameters;
        }

        public RsaKeyIdentifierClause(RSA rsa) :
            base(null)
        {
            this.rsa = rsa;
            parameters = rsa.ExportParameters(false);
        }

        public override bool CanCreateKey => true;

        public override SecurityKey CreateKey()
        {
            if (key == null)
            {
                key = new RsaSecurityKey(Rsa);
            }
            return key;
        }

        public byte[] GetExponent()
        {
            return parameters.Exponent;
        }

        public byte[] GetModulus()
        {
            return parameters.Modulus;
        }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (keyIdentifierClause == null)
            {
                throw new ArgumentNullException(nameof(keyIdentifierClause));
            }
            return keyIdentifierClause is RsaKeyIdentifierClause otherClause &&
                Matches(otherClause.parameters);
        }

        public bool Matches(RSAParameters p2)
        {
            var p1 = parameters;
            return CompareHelper.ByteArraysEqual(p1.Modulus, p2.Modulus) &&
                CompareHelper.ByteArraysEqual(p1.Exponent, p2.Exponent);
        }
    }
}

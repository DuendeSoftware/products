// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;

namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    static class EcUtils
    {
        public static bool ByteArraysEqual(byte[] a, byte[] b) =>
            CompareHelper.ByteArraysEqual(a, b);

        static bool PointsEqual(ECPoint a, ECPoint b)
        {
            return ByteArraysEqual(a.X, b.X) && ByteArraysEqual(a.Y, b.Y);
        }

        static bool CurvesEqual(ECCurve a, ECCurve b)
        {
            return
                ByteArraysEqual(a.A, b.A) &&
                ByteArraysEqual(a.B, b.B) &&
                ByteArraysEqual(a.Cofactor, b.Cofactor) &&
                a.CurveType == b.CurveType &&
                PointsEqual(a.G, b.G) &&
                ByteArraysEqual(a.Order, b.Order) &&
                ByteArraysEqual(a.Polynomial, b.Polynomial) &&
                ByteArraysEqual(a.Prime, b.Prime) &&
                ByteArraysEqual(a.Seed, b.Seed);
        }

        public static bool ParametersEqual(ECParameters a, ECParameters b)
        {
            return CurvesEqual(a.Curve, b.Curve) &&
                ByteArraysEqual(a.D, b.D) &&
                PointsEqual(a.Q, b.Q);
        }

        public static HashAlgorithmName GetHashAlgorithmName(string name)
        {
            switch (name)
            {
                case "SHA1":
                    return HashAlgorithmName.SHA1;
                case "SHA256":
                    return HashAlgorithmName.SHA256;
                case "SHA384":
                    return HashAlgorithmName.SHA384;
                case "SHA512":
                    return HashAlgorithmName.SHA512;
                default:
                    throw new CryptographicException($"Unknown hash algorithm '{name}'");
            }
        }

        public static ECDsa ValidateEcDsaKey(AsymmetricAlgorithm key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            var ecKey = key as ECDsa;
            if (ecKey == null)
            {
                throw new InvalidOperationException($"SetKey called with a key of type other than ECDsa: {key.GetType()}");
            }
            return ecKey;
        }
    }

    internal class EcSignatureDeformatter : AsymmetricSignatureDeformatter
    {
        ECDsa ecDsa;
        HashAlgorithmName hashAlgorithmName;

        public EcSignatureDeformatter()
        {
        }

        public EcSignatureDeformatter(ECDsa key, HashAlgorithmName algorithm)
        {
            ecDsa = key;
            hashAlgorithmName = algorithm;
        }

        public override void SetHashAlgorithm(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            hashAlgorithmName = EcUtils.GetHashAlgorithmName(name);
        }

        public override void SetKey(AsymmetricAlgorithm key)
        {
            ecDsa = EcUtils.ValidateEcDsaKey(key);
        }

        public override bool VerifySignature(byte[] rgbHash, byte[] rgbSignature)
        {
            if (rgbHash == null)
            {
                throw new ArgumentNullException(nameof(rgbHash));
            }
            if (rgbSignature == null)
            {
                throw new ArgumentNullException(nameof(rgbSignature));
            }
            if (ecDsa == null)
            {
                throw new InvalidOperationException("VerifySignature called with no key set");
            }
            return ecDsa.VerifyData(rgbHash, rgbSignature, hashAlgorithmName);
        }
    }

    internal class EcSignatureFormatter : AsymmetricSignatureFormatter
    {
        ECDsa ecDsa;
        HashAlgorithmName hashAlgorithmName;

        public EcSignatureFormatter()
        {
        }

        public EcSignatureFormatter(ECDsa key, HashAlgorithmName algorithm)
        {
            ecDsa = key;
            hashAlgorithmName = algorithm;
        }

        public override byte[] CreateSignature(byte[] rgbHash)
        {
            if (rgbHash == null)
            {
                throw new ArgumentNullException(nameof(rgbHash));
            }
            if (ecDsa == null)
            {
                throw new InvalidOperationException("VerifySignature called with no key set");
            }
            return ecDsa.SignHash(rgbHash);
        }

        public override void SetHashAlgorithm(string name)
        {
            hashAlgorithmName = EcUtils.GetHashAlgorithmName(name);
        }

        public override void SetKey(AsymmetricAlgorithm key)
        {
            ecDsa = EcUtils.ValidateEcDsaKey(key);
        }
    }

    internal class EcSecurityKey : AsymmetricSecurityKey
    {
        ECDsa ecDsa;

        public EcSecurityKey(ECParameters parameters)
        {
            ecDsa = ECDsa.Create(parameters);
        }

        public override AsymmetricAlgorithm GetAsymmetricAlgorithm(string algorithm, bool privateKey)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }

            switch (algorithm)
            {
                case SecurityAlgorithms.EcDsaSha1:
                case SecurityAlgorithms.EcDsaSha224:
                case SecurityAlgorithms.EcDsaSha256:
                case SecurityAlgorithms.EcDsaSha384:
                case SecurityAlgorithms.EcDsaSha512:
                    return ecDsa;
            }

            throw new NotSupportedException($"EcSecurityKey does not support the algorithm '{algorithm}'");
        }

        public override HashAlgorithm GetHashAlgorithmForSignature(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SecurityAlgorithms.EcDsaSha1:
                    return new SHA1Managed();
                case SecurityAlgorithms.EcDsaSha256:
                    return new SHA256Managed();
                //case SecurityAlgorithms.EcDsaSha224:
                //	return new SHA224Managed();
                case SecurityAlgorithms.EcDsaSha384:
                    return new SHA384Managed();
                case SecurityAlgorithms.EcDsaSha512:
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
            HashAlgorithmName hashAlgorithmName = EcUtils.GetHashAlgorithmName(algorithm);
            return new EcSignatureDeformatter(ecDsa, hashAlgorithmName);
        }

        public override AsymmetricSignatureFormatter GetSignatureFormatter(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            HashAlgorithmName hashAlgorithmName = EcUtils.GetHashAlgorithmName(algorithm);
            return new EcSignatureFormatter(ecDsa, hashAlgorithmName);
        }

        public override int KeySize => ecDsa.KeySize;

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

            throw new NotSupportedException("The EC key does not support encryption");
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

            throw new NotSupportedException("The EC key does not support encryption");
        }

        public override bool IsAsymmetricAlgorithm(string algorithm)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }
            switch (algorithm)
            {
                case SecurityAlgorithms.EcDsaSha1:
                case SecurityAlgorithms.EcDsaSha224:
                case SecurityAlgorithms.EcDsaSha256:
                case SecurityAlgorithms.EcDsaSha384:
                case SecurityAlgorithms.EcDsaSha512:
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
                case SecurityAlgorithms.EcDsaSha1:
                case SecurityAlgorithms.EcDsaSha224:
                case SecurityAlgorithms.EcDsaSha256:
                case SecurityAlgorithms.EcDsaSha384:
                case SecurityAlgorithms.EcDsaSha512:
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
            return false;
        }

        public override bool HasPrivateKey()
        {
            return true;
        }
    }

    internal class EcKeyIdentifierClause : SecurityKeyIdentifierClause
    {
        public ECParameters Parameters { get; private set; }

        public EcKeyIdentifierClause(ECParameters parameters) :
            base(null)
        {
            Parameters = parameters;
        }

        public override bool CanCreateKey => true;

        public override SecurityKey CreateKey()
        {
            return new EcSecurityKey(Parameters);
        }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (keyIdentifierClause == null)
            {
                throw new ArgumentNullException(nameof(keyIdentifierClause));
            }
            return keyIdentifierClause is EcKeyIdentifierClause otherClause &&
                Matches(otherClause.Parameters);
        }

        public bool Matches(ECParameters other)
        {
            return EcUtils.ParametersEqual(Parameters, other);
        }
    }
}

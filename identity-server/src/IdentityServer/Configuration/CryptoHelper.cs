// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Duende.IdentityModel;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Crypto helper
/// </summary>
public static class CryptoHelper
{
    /// <summary>
    /// Creates a new RSA security key.
    /// </summary>
    /// <returns></returns>
#pragma warning disable CA2000 // See: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/1433
    public static RsaSecurityKey CreateRsaSecurityKey(int keySize = 2048) => new RsaSecurityKey(RSA.Create(keySize))
#pragma warning restore CA2000
    {
        KeyId = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)
    };

    /// <summary>
    /// Creates a new ECDSA security key.
    /// </summary>
    /// <param name="curve">The name of the curve as defined in
    /// https://tools.ietf.org/html/rfc7518#section-6.2.1.1.</param>
    /// <returns></returns>
#pragma warning disable CA2000 // See: https://github.com/dotnet/runtime/issues/99605
    public static ECDsaSecurityKey CreateECDsaSecurityKey(string curve = JsonWebKeyECTypes.P256) => new ECDsaSecurityKey(ECDsa.Create(GetCurveFromCrvValue(curve)))
#pragma warning restore CA2000
    {
        KeyId = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)
    };

    /// <summary>
    /// Creates an RSA security key.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="id">The identifier.</param>
    /// <returns></returns>
    public static RsaSecurityKey CreateRsaSecurityKey(RSAParameters parameters, string id)
    {
        var key = new RsaSecurityKey(parameters)
        {
            KeyId = id
        };

        return key;
    }

    /// <summary>
    /// Creates the hash for the various hash claims (e.g. c_hash, at_hash or s_hash).
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="tokenSigningAlgorithm">The token signing algorithm</param>
    /// <returns></returns>
    public static string CreateHashClaimValue(string value, string tokenSigningAlgorithm)
    {
        var (hashFunction, hashLength) = GetHashFunctionForSigningAlgorithm(tokenSigningAlgorithm);
        var encodedBytes = Encoding.ASCII.GetBytes(value);
        var hash = hashFunction(encodedBytes);

        var size = (hashLength / 8) / 2;

        var leftPart = new byte[size];
        Array.Copy(hash, leftPart, size);

        return Base64Url.EncodeToString(leftPart);
    }

    /// <summary>
    /// Returns the matching hash function for a token signing algorithm
    /// </summary>
    /// <param name="signingAlgorithm"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static (Func<byte[], byte[]> hashFunction, int hashLength) GetHashFunctionForSigningAlgorithm(string signingAlgorithm)
    {
        if (signingAlgorithm.Length < 3)
        {
            throw new InvalidOperationException($"Invalid signing algorithm: {signingAlgorithm}");
        }

        var algorithm = signingAlgorithm.AsSpan().Trim()[^3..];

        if (int.TryParse(algorithm, out var hashLength))
        {
            Func<byte[], byte[]> hashFunction = hashLength switch
            {
                256 => SHA256.HashData,
                384 => SHA384.HashData,
                512 => SHA512.HashData,
                _ => throw new InvalidOperationException($"Invalid signing algorithm: {signingAlgorithm}"),
            };

            return (hashFunction, hashLength);
        }

        throw new InvalidOperationException($"Invalid signing algorithm: {signingAlgorithm}");
    }

    /// <summary>
    /// Returns the matching hashing algorithm for a token signing algorithm
    /// </summary>
    /// <param name="signingAlgorithm">The signing algorithm</param>
    /// <returns></returns>
    [Obsolete("This method is obsolete and will be removed in a future version. Consider using GetHashFunctionForSigningAlgorithm instead for better performance (it does not allocate a HashAlgorithm)")]
    public static HashAlgorithm GetHashAlgorithmForSigningAlgorithm(string signingAlgorithm)
    {
        var signingAlgorithmBits = int.Parse(signingAlgorithm.AsSpan()[^3..], CultureInfo.InvariantCulture);

        return signingAlgorithmBits switch
        {
            256 => SHA256.Create(),
            384 => SHA384.Create(),
            512 => SHA512.Create(),
            _ => throw new InvalidOperationException($"Invalid signing algorithm: {signingAlgorithm}"),
        };
    }

    /// <summary>
    /// Returns the matching named curve for RFC 7518 crv value
    /// </summary>
    internal static ECCurve GetCurveFromCrvValue(string crv) => crv switch
    {
        JsonWebKeyECTypes.P256 => ECCurve.NamedCurves.nistP256,
        JsonWebKeyECTypes.P384 => ECCurve.NamedCurves.nistP384,
        JsonWebKeyECTypes.P521 => ECCurve.NamedCurves.nistP521,
        _ => throw new InvalidOperationException($"Unsupported curve type of {crv}"),
    };

    /// <summary>
    /// Returns the matching curve name for signing algorithm.
    /// </summary>
    internal static string? GetCurveNameFromSigningAlgorithm(string alg) => alg switch
    {
        "ES256" => "P-256",
        "ES384" => "P-384",
        "ES512" => "P-521",
        _ => null
    };

    /// <summary>
    /// Return the matching RFC 7518 crv value for curve
    /// </summary>
    internal static string GetCrvValueFromCurve(ECCurve curve) => curve.Oid.Value switch
    {
        Constants.CurveOids.P256 => JsonWebKeyECTypes.P256,
        Constants.CurveOids.P384 => JsonWebKeyECTypes.P384,
        Constants.CurveOids.P521 => JsonWebKeyECTypes.P521,
        _ => throw new InvalidOperationException($"Unsupported curve type of {curve.Oid.Value} - {curve.Oid.FriendlyName}"),
    };

    internal static bool IsValidCurveForAlgorithm(ECDsaSecurityKey key, string algorithm)
    {
        var parameters = key.ECDsa.ExportParameters(false);

        if (algorithm == SecurityAlgorithms.EcdsaSha256 && parameters.Curve.Oid.Value != Constants.CurveOids.P256
            || algorithm == SecurityAlgorithms.EcdsaSha384 && parameters.Curve.Oid.Value != Constants.CurveOids.P384
            || algorithm == SecurityAlgorithms.EcdsaSha512 && parameters.Curve.Oid.Value != Constants.CurveOids.P521)
        {
            return false;
        }

        return true;
    }

    internal static bool IsValidCrvValueForAlgorithm(string crv) => crv == JsonWebKeyECTypes.P256 ||
               crv == JsonWebKeyECTypes.P384 ||
               crv == JsonWebKeyECTypes.P521;

    internal static string GetRsaSigningAlgorithmValue(IdentityServerConstants.RsaSigningAlgorithm value) => value switch
    {
        IdentityServerConstants.RsaSigningAlgorithm.RS256 => SecurityAlgorithms.RsaSha256,
        IdentityServerConstants.RsaSigningAlgorithm.RS384 => SecurityAlgorithms.RsaSha384,
        IdentityServerConstants.RsaSigningAlgorithm.RS512 => SecurityAlgorithms.RsaSha512,

        IdentityServerConstants.RsaSigningAlgorithm.PS256 => SecurityAlgorithms.RsaSsaPssSha256,
        IdentityServerConstants.RsaSigningAlgorithm.PS384 => SecurityAlgorithms.RsaSsaPssSha384,
        IdentityServerConstants.RsaSigningAlgorithm.PS512 => SecurityAlgorithms.RsaSsaPssSha512,
        _ => throw new ArgumentException("Invalid RSA signing algorithm value", nameof(value)),
    };

    internal static string GetECDsaSigningAlgorithmValue(IdentityServerConstants.ECDsaSigningAlgorithm value) => value switch
    {
        IdentityServerConstants.ECDsaSigningAlgorithm.ES256 => SecurityAlgorithms.EcdsaSha256,
        IdentityServerConstants.ECDsaSigningAlgorithm.ES384 => SecurityAlgorithms.EcdsaSha384,
        IdentityServerConstants.ECDsaSigningAlgorithm.ES512 => SecurityAlgorithms.EcdsaSha512,
        _ => throw new ArgumentException("Invalid ECDsa signing algorithm value", nameof(value)),
    };

    internal static X509Certificate2? FindCertificate(string name, StoreLocation location, NameType nameType)
    {
        X509Certificate2? certificate = null;

        if (location == StoreLocation.LocalMachine)
        {
            if (nameType == NameType.SubjectDistinguishedName)
            {
                certificate = X509.LocalMachine.My.SubjectDistinguishedName.Find(name, validOnly: false).FirstOrDefault();
            }
            else if (nameType == NameType.Thumbprint)
            {
                certificate = X509.LocalMachine.My.Thumbprint.Find(name, validOnly: false).FirstOrDefault();
            }
        }
        else
        {
            if (nameType == NameType.SubjectDistinguishedName)
            {
                certificate = X509.CurrentUser.My.SubjectDistinguishedName.Find(name, validOnly: false).FirstOrDefault();
            }
            else if (nameType == NameType.Thumbprint)
            {
                certificate = X509.CurrentUser.My.Thumbprint.Find(name, validOnly: false).FirstOrDefault();
            }
        }

        return certificate;
    }
}

/// <summary>
/// Describes the string so we know what to search for in certificate store
/// </summary>
public enum NameType
{
    /// <summary>
    /// subject distinguished name
    /// </summary>
    SubjectDistinguishedName,

    /// <summary>
    /// thumbprint
    /// </summary>
    Thumbprint
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Extracts a public key from an <see cref="X509Certificate2"/> and encodes it
/// as a COSE key in CBOR format, compatible with <see cref="ISignatureVerifier"/>.
/// </summary>
internal static class CoseKeyExporter
{
    /// <summary>
    /// Number of COSE key map entries for an EC2 key (kty, alg, crv, x, y).
    /// </summary>
    private const int Ec2CoseMapSize = 5;

    /// <summary>
    /// Number of COSE key map entries for an RSA key (kty, alg, n, e).
    /// </summary>
    private const int RsaCoseMapSize = 4;
    /// <summary>
    /// Extracts the public key from the certificate and encodes it as COSE CBOR bytes.
    /// </summary>
    /// <param name="certificate">The X.509 certificate containing the public key.</param>
    /// <param name="algorithm">The COSE algorithm identifier (e.g., -7 for ES256, -257 for RS256).</param>
    /// <param name="coseKeyBytes">The COSE-encoded public key bytes when successful.</param>
    /// <returns><c>true</c> if the key was successfully exported; otherwise, <c>false</c>.</returns>
    internal static bool TryExport(
        X509Certificate2 certificate,
        int algorithm,
        [NotNullWhen(true)] out byte[]? coseKeyBytes)
    {
        coseKeyBytes = null;

        try
        {
            // Dispatch on the certificate's key type OID rather than the COSE algorithm.
            // This allows any algorithm within a key family (e.g., ES256/ES384/ES512 for EC,
            // RS256/RS384/PS256 for RSA) to work automatically when a customer registers
            // a matching ISignatureVerifier.
            coseKeyBytes = certificate.PublicKey.Oid.Value switch
            {
                WebAuthnConstants.Oids.EcPublicKey => ExportEc2Key(certificate, algorithm),
                WebAuthnConstants.Oids.RsaEncryption => ExportRsaKey(certificate, algorithm),
                _ => null
            };

            return coseKeyBytes is not null;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static byte[]? ExportEc2Key(X509Certificate2 certificate, int algorithm)
    {
        using var ecdsaKey = certificate.GetECDsaPublicKey();
        if (ecdsaKey is null)
        {
            return null;
        }

        var parameters = ecdsaKey.ExportParameters(false);

        if (parameters.Q.X is null || parameters.Q.Y is null)
        {
            return null;
        }

        var curve = GetCoseCurve(parameters.Curve);
        if (curve is null)
        {
            return null;
        }

        var writer = new CborWriter();
        writer.WriteStartMap(Ec2CoseMapSize);

        writer.WriteInt32(CoseConstants.Labels.KeyType);
        writer.WriteInt32(CoseConstants.KeyTypes.Ec2);

        writer.WriteInt32(CoseConstants.Labels.Algorithm);
        writer.WriteInt32(algorithm);

        writer.WriteInt32(CoseConstants.Labels.EcCurve);
        writer.WriteInt32(curve.Value);

        writer.WriteInt32(CoseConstants.Labels.EcX);
        writer.WriteByteString(parameters.Q.X);

        writer.WriteInt32(CoseConstants.Labels.EcY);
        writer.WriteByteString(parameters.Q.Y);

        writer.WriteEndMap();
        return writer.Encode();
    }

    private static byte[]? ExportRsaKey(X509Certificate2 certificate, int algorithm)
    {
        using var rsaKey = certificate.GetRSAPublicKey();
        if (rsaKey is null)
        {
            return null;
        }

        var parameters = rsaKey.ExportParameters(false);

        if (parameters.Modulus is null || parameters.Exponent is null)
        {
            return null;
        }

        var writer = new CborWriter();
        writer.WriteStartMap(RsaCoseMapSize);

        writer.WriteInt32(CoseConstants.Labels.KeyType);
        writer.WriteInt32(CoseConstants.KeyTypes.Rsa);

        writer.WriteInt32(CoseConstants.Labels.Algorithm);
        writer.WriteInt32(algorithm);

        writer.WriteInt32(CoseConstants.Labels.RsaModulus);
        writer.WriteByteString(parameters.Modulus);

        writer.WriteInt32(CoseConstants.Labels.RsaExponent);
        writer.WriteByteString(parameters.Exponent);

        writer.WriteEndMap();
        return writer.Encode();
    }

    private static int? GetCoseCurve(ECCurve curve)
    {
        // Match by OID since ECCurve.Equals is not reliable across platforms.
        var oid = curve.Oid?.Value;

        if (oid == ECCurve.NamedCurves.nistP256.Oid.Value)
        {
            return CoseConstants.Curves.P256;
        }

        if (oid == ECCurve.NamedCurves.nistP384.Oid.Value)
        {
            return CoseConstants.Curves.P384;
        }

        if (oid == ECCurve.NamedCurves.nistP521.Oid.Value)
        {
            return CoseConstants.Curves.P521;
        }

        return null;
    }
}

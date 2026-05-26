// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal static class TpmStructures
{

    /// <summary>
    /// Attempts to compute a hash using the TPM hash algorithm identifier to select the .NET implementation.
    /// Returns false if the algorithm is not supported.
    /// </summary>
    internal static bool TryHashData(ushort tpmHashAlgorithm, ReadOnlySpan<byte> data,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? hash)
    {
        hash = tpmHashAlgorithm switch
        {
#pragma warning disable CA5350 // SHA-1 is required for compatibility with older TPM hardware
            TpmConstants.AlgSha1 => SHA1.HashData(data),
#pragma warning restore CA5350
            TpmConstants.AlgSha256 => SHA256.HashData(data),
            TpmConstants.AlgSha384 => SHA384.HashData(data),
            TpmConstants.AlgSha512 => SHA512.HashData(data),
            _ => null
        };
        return hash is not null;
    }

    /// <summary>
    /// Maps a COSE algorithm identifier to the corresponding TPM hash algorithm identifier.
    /// Per WebAuthn §8.3, the hash used for extraData validation must be the one employed by the COSE 'alg'.
    /// </summary>
    internal static ushort? GetTpmHashAlgorithmForCoseAlg(int coseAlgorithm) =>
        coseAlgorithm switch
        {
            CoseAlgorithms.Rs1 => TpmConstants.AlgSha1,
            CoseAlgorithms.Rs256 or CoseAlgorithms.Es256 or CoseAlgorithms.Ps256 => TpmConstants.AlgSha256,
            CoseAlgorithms.Rs384 or CoseAlgorithms.Es384 or CoseAlgorithms.Ps384 => TpmConstants.AlgSha384,
            CoseAlgorithms.Rs512 or CoseAlgorithms.Es512 or CoseAlgorithms.Ps512 => TpmConstants.AlgSha512,
            _ => null
        };

    internal static bool ValidateExtraData(TpmsAttest certInfo, byte[] authData, byte[] clientDataHash,
        ushort tpmHashAlgorithm)
    {
        var signedData = WebAuthnCrypto.CombineBytes(authData, clientDataHash);
        if (!TryHashData(tpmHashAlgorithm, signedData, out var expectedExtraData))
        {
            return false;
        }

        return certInfo.ExtraData.AsSpan().SequenceEqual(expectedExtraData);
    }

    internal static bool ValidateAttestedName(TpmsAttest certInfo, TpmtPublic pubArea, byte[] pubAreaBytes)
    {
        var expectedName = pubArea.ComputeName(pubAreaBytes);
        return certInfo.Attested.Name.AsSpan().SequenceEqual(expectedName);
    }

    internal static bool ValidatePublicKeyMatch(TpmtPublic pubArea, CoseKey credentialPublicKey) =>
        pubArea.Type switch
        {
            TpmConstants.AlgRsa => ValidateRsaPublicKeyMatch(pubArea, credentialPublicKey),
            TpmConstants.AlgEcc => ValidateEccPublicKeyMatch(pubArea, credentialPublicKey),
            _ => false
        };

    private static bool ValidateRsaPublicKeyMatch(TpmtPublic pubArea, CoseKey coseKey)
    {
        if (pubArea.RsaParameters is null || pubArea.Unique.RsaModulus is null)
        {
            return false;
        }

        if (coseKey.RsaModulus is null || coseKey.RsaExponent is null)
        {
            return false;
        }

        return NormalizeUnsignedInteger(pubArea.Unique.RsaModulus)
                   .SequenceEqual(NormalizeUnsignedInteger(coseKey.RsaModulus))
               && GetRsaExponentBytes(pubArea.RsaParameters.Exponent)
                   .SequenceEqual(NormalizeUnsignedInteger(coseKey.RsaExponent));
    }

    private static bool ValidateEccPublicKeyMatch(TpmtPublic pubArea, CoseKey coseKey)
    {
        if (pubArea.EccParameters is null || pubArea.Unique.EccX is null || pubArea.Unique.EccY is null)
        {
            return false;
        }

        if (coseKey.EcCurve is null || coseKey.EcX is null || coseKey.EcY is null)
        {
            return false;
        }

        if (!TryMapTpmCurveIdToCoseCurve(pubArea.EccParameters.CurveId, out var expectedCoseCurve))
        {
            return false;
        }

        return coseKey.EcCurve.Value == expectedCoseCurve
               && pubArea.Unique.EccX.AsSpan().SequenceEqual(coseKey.EcX)
               && pubArea.Unique.EccY.AsSpan().SequenceEqual(coseKey.EcY);
    }

    private static bool TryMapTpmCurveIdToCoseCurve(ushort tpmCurveId, out int coseCurve)
    {
        coseCurve = tpmCurveId switch
        {
            TpmConstants.EccCurveNistP256 => CoseConstants.Curves.P256,
            TpmConstants.EccCurveNistP384 => CoseConstants.Curves.P384,
            TpmConstants.EccCurveNistP521 => CoseConstants.Curves.P521,
            _ => default
        };

        return coseCurve != default;
    }

    private static byte[] GetRsaExponentBytes(uint exponent)
    {
        var actualExponent = exponent == 0 ? TpmConstants.DefaultRsaExponent : exponent;
        Span<byte> exponentBytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(exponentBytes, actualExponent);
        return NormalizeUnsignedInteger(exponentBytes);
    }

    private static byte[] NormalizeUnsignedInteger(ReadOnlySpan<byte> value)
    {
        var offset = 0;

        while (offset < value.Length - 1 && value[offset] == 0)
        {
            offset++;
        }

        return value[offset..].ToArray();
    }
}

// Note: The WebAuthn spec (§8.3) describes the 'sig' field as a TPMT_SIGNATURE structure,
// but in practice authenticators (notably Windows Hello) send raw signature bytes.
// We therefore treat 'sig' as opaque bytes passed directly to signature verification,
// other libraries do a similar thing

internal sealed record TpmsAttest(
    uint Magic,
    ushort Type,
    byte[] QualifiedSigner,
    byte[] ExtraData,
    TpmsClockInfo ClockInfo,
    ulong FirmwareVersion,
    TpmsCertifyInfo Attested)
{
    internal static TpmsAttest Parse(byte[] bytes) => Parse(bytes.AsSpan());

    private static TpmsAttest Parse(ReadOnlySpan<byte> bytes)
    {
        var offset = 0;

        var magic = TpmBinaryReader.ReadUInt32(bytes, ref offset);
        if (magic != TpmConstants.GeneratedValue)
        {
            throw new FormatException($"Invalid TPMS_ATTEST magic value 0x{magic:x8}.");
        }

        var type = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        if (type != TpmConstants.StAttestCertify)
        {
            throw new FormatException($"Invalid TPMS_ATTEST type value 0x{type:x4}.");
        }

        var qualifiedSigner = TpmBinaryReader.ReadTpm2B(bytes, ref offset);
        var extraData = TpmBinaryReader.ReadTpm2B(bytes, ref offset);
        var clockInfo = TpmsClockInfo.Parse(bytes, ref offset);
        var firmwareVersion = TpmBinaryReader.ReadUInt64(bytes, ref offset);
        var attested = TpmsCertifyInfo.Parse(bytes, ref offset);

        TpmBinaryReader.EnsureFullyRead(bytes, offset);

        return new TpmsAttest(magic, type, qualifiedSigner, extraData, clockInfo, firmwareVersion, attested);
    }
}

internal sealed record TpmsClockInfo(ulong Clock, uint ResetCount, uint RestartCount, byte Safe)
{
    internal static TpmsClockInfo Parse(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var clock = TpmBinaryReader.ReadUInt64(bytes, ref offset);
        var resetCount = TpmBinaryReader.ReadUInt32(bytes, ref offset);
        var restartCount = TpmBinaryReader.ReadUInt32(bytes, ref offset);
        var safe = TpmBinaryReader.ReadByte(bytes, ref offset);
        return new TpmsClockInfo(clock, resetCount, restartCount, safe);
    }
}

internal sealed record TpmsCertifyInfo(byte[] Name, byte[] QualifiedName)
{
    internal static TpmsCertifyInfo Parse(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var name = TpmBinaryReader.ReadTpm2B(bytes, ref offset);
        var qualifiedName = TpmBinaryReader.ReadTpm2B(bytes, ref offset);
        return new TpmsCertifyInfo(name, qualifiedName);
    }
}

internal sealed record TpmtPublic(
    ushort Type,
    ushort NameAlg,
    uint ObjectAttributes,
    byte[] AuthPolicy,
    byte[] Parameters,
    TpmPublicUnique Unique,
    TpmsRsaParameters? RsaParameters,
    TpmsEccParameters? EccParameters)
{
    internal static TpmtPublic Parse(byte[] bytes) => Parse(bytes.AsSpan());

    private static TpmtPublic Parse(ReadOnlySpan<byte> bytes)
    {
        var offset = 0;

        var type = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        if (type != TpmConstants.AlgRsa && type != TpmConstants.AlgEcc)
        {
            throw new FormatException($"Unsupported TPMT_PUBLIC type value 0x{type:x4}.");
        }

        var nameAlg = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        TpmBinaryReader.EnsureSupportedNameAlg(nameAlg);

        var objectAttributes = TpmBinaryReader.ReadUInt32(bytes, ref offset);
        var authPolicy = TpmBinaryReader.ReadTpm2B(bytes, ref offset);

        var parametersOffset = offset;
        TpmsRsaParameters? rsaParameters = null;
        TpmsEccParameters? eccParameters = null;
        TpmPublicUnique unique;

        if (type == TpmConstants.AlgRsa)
        {
            rsaParameters = TpmsRsaParameters.Parse(bytes, ref offset);
            var parameters = bytes[parametersOffset..offset].ToArray();
            var modulus = TpmBinaryReader.ReadTpm2B(bytes, ref offset);
            unique = new TpmPublicUnique(modulus, null, null);

            TpmBinaryReader.EnsureFullyRead(bytes, offset);
            return new TpmtPublic(type, nameAlg, objectAttributes, authPolicy, parameters, unique, rsaParameters, null);
        }

        eccParameters = TpmsEccParameters.Parse(bytes, ref offset);
        var eccParametersBytes = bytes[parametersOffset..offset].ToArray();
        var x = TpmBinaryReader.ReadTpm2B(bytes, ref offset);
        var y = TpmBinaryReader.ReadTpm2B(bytes, ref offset);
        unique = new TpmPublicUnique(null, x, y);

        TpmBinaryReader.EnsureFullyRead(bytes, offset);
        return new TpmtPublic(type, nameAlg, objectAttributes, authPolicy, eccParametersBytes, unique, null,
            eccParameters);
    }

    internal byte[] ComputeName(byte[] pubAreaBytes)
    {
        if (!TpmStructures.TryHashData(NameAlg, pubAreaBytes, out var digest))
        {
            throw new FormatException($"Unsupported TPM nameAlg value 0x{NameAlg:x4}.");
        }

        var name = new byte[sizeof(ushort) + digest.Length];
        BinaryPrimitives.WriteUInt16BigEndian(name, NameAlg);
        digest.CopyTo(name.AsSpan(sizeof(ushort)));
        return name;
    }
}

internal sealed record TpmsRsaParameters(ushort Symmetric, ushort Scheme, ushort KeyBits, uint Exponent)
{
    internal static TpmsRsaParameters Parse(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var symmetric = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        EnsureAlgNull(symmetric, nameof(Symmetric));
        var scheme = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        EnsureAlgNull(scheme, nameof(Scheme));
        var keyBits = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        var exponent = TpmBinaryReader.ReadUInt32(bytes, ref offset);
        return new TpmsRsaParameters(symmetric, scheme, keyBits, exponent);
    }

    private static void EnsureAlgNull(ushort value, string fieldName)
    {
        if (value != TpmConstants.AlgNull)
        {
            throw new FormatException(
                $"TPMS_RSA_PARMS {fieldName} field must be TPM_ALG_NULL (0x{TpmConstants.AlgNull:x4}), but was 0x{value:x4}.");
        }
    }
}

internal sealed record TpmsEccParameters(ushort Symmetric, ushort Scheme, ushort CurveId, ushort Kdf)
{
    internal static TpmsEccParameters Parse(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var symmetric = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        EnsureAlgNull(symmetric, nameof(Symmetric));
        var scheme = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        EnsureAlgNull(scheme, nameof(Scheme));
        var curveId = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        var kdf = TpmBinaryReader.ReadUInt16(bytes, ref offset);
        return new TpmsEccParameters(symmetric, scheme, curveId, kdf);
    }

    private static void EnsureAlgNull(ushort value, string fieldName)
    {
        if (value != TpmConstants.AlgNull)
        {
            throw new FormatException(
                $"TPMS_ECC_PARMS {fieldName} field must be TPM_ALG_NULL (0x{TpmConstants.AlgNull:x4}), but was 0x{value:x4}.");
        }
    }
}

internal sealed record TpmPublicUnique(byte[]? RsaModulus, byte[]? EccX, byte[]? EccY);

internal static class TpmBinaryReader
{
    internal static ushort ReadUInt16(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
        offset += sizeof(ushort);
        return value;
    }

    internal static uint ReadUInt32(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
        offset += sizeof(uint);
        return value;
    }

    internal static ulong ReadUInt64(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(ulong));
        var value = BinaryPrimitives.ReadUInt64BigEndian(bytes[offset..]);
        offset += sizeof(ulong);
        return value;
    }

    internal static byte ReadByte(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(byte));
        var value = bytes[offset];
        offset += sizeof(byte);
        return value;
    }

    internal static byte[] ReadTpm2B(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var length = ReadUInt16(bytes, ref offset);
        EnsureAvailable(bytes, offset, length);
        var value = bytes[offset..(offset + length)].ToArray();
        offset += length;
        return value;
    }

    internal static void EnsureSupportedNameAlg(ushort nameAlg)
    {
        if (nameAlg != TpmConstants.AlgSha1 &&
            nameAlg != TpmConstants.AlgSha256 &&
            nameAlg != TpmConstants.AlgSha384 &&
            nameAlg != TpmConstants.AlgSha512)
        {
            throw new FormatException($"Unsupported TPM nameAlg value 0x{nameAlg:x4}.");
        }
    }

    internal static void EnsureFullyRead(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset != bytes.Length)
        {
            throw new FormatException("TPM structure contains trailing bytes.");
        }
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (bytes.Length - offset < length)
        {
            throw new FormatException("TPM structure is truncated.");
        }
    }
}

/// <summary>
/// Constants from the TPM 2.0 specification used during attestation verification.
/// Reference: TPM 2.0 Part 2: Structures
/// https://trustedcomputinggroup.org/resource/tpm-library-specification/
/// </summary>
internal static class TpmConstants
{
    // TPM_GENERATED_VALUE - must appear in TPMS_ATTEST.magic to prove TPM origin.
    // See Part 2, section 6.2 (TPM_GENERATED).
    internal const uint GeneratedValue = 0xff544347;

    // TPM_ST_ATTEST_CERTIFY - attestation statement type for key certification.
    // See Part 2, section 6.9 (TPMI_ST_ATTEST) and 6.4 (TPM_ST).
    internal const ushort StAttestCertify = 0x8017;

    // Algorithm identifiers (TPM_ALG_ID). See Part 2, section 6.3.
    internal const ushort AlgNull = 0x0010;
    internal const ushort AlgSha1 = 0x0004;
    internal const ushort AlgRsa = 0x0001;
    internal const ushort AlgSha256 = 0x000B;
    internal const ushort AlgSha384 = 0x000C;
    internal const ushort AlgSha512 = 0x000D;
    internal const ushort AlgEcc = 0x0023;

    // ECC curve identifiers (TPM_ECC_CURVE). See Part 2, section 6.5.
    internal const ushort EccCurveNistP256 = 0x0003;
    internal const ushort EccCurveNistP384 = 0x0004;
    internal const ushort EccCurveNistP521 = 0x0005;

    // TPMA_OBJECT bit for sign/encrypt capability. See Part 2, section 8.3.
    internal const uint ObjectAttributeSignEncrypt = 0x00040000;

    // Default RSA public exponent (e = 65537). When TPMS_RSA_PARMS.exponent is 0,
    // the TPM uses this value. See Part 2, section 11.2.4 (TPMS_RSA_PARMS).
    internal const uint DefaultRsaExponent = 65537;
}

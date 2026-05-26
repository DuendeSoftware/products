// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Parsed COSE key with algorithm info, typed key parameters, and raw CBOR for storage.
/// </summary>
internal sealed record CoseKey(
    int KeyType,
    int Algorithm,
    IReadOnlyCollection<byte> RawCbor)
{
    private const int LabelKeyType = 1;
    private const int LabelAlgorithm = 3;
    private const int LabelParam1 = -1; // EC: crv, RSA: n
    private const int LabelParam2 = -2; // EC: x, RSA: e
    private const int LabelParam3 = -3; // EC: y

    /// <summary>
    /// EC2: Elliptic curve identifier (COSE label -1). Null for non-EC keys.
    /// </summary>
    internal int? EcCurve { get; init; }

    /// <summary>
    /// EC2: x-coordinate (COSE label -2). Null for non-EC keys.
    /// </summary>
    internal byte[]? EcX { get; init; }

    /// <summary>
    /// EC2: y-coordinate (COSE label -3). Null for non-EC keys.
    /// </summary>
    internal byte[]? EcY { get; init; }

    /// <summary>
    /// RSA: Modulus n (COSE label -1). Null for non-RSA keys.
    /// </summary>
    internal byte[]? RsaModulus { get; init; }

    /// <summary>
    /// RSA: Public exponent e (COSE label -2). Null for non-RSA keys.
    /// </summary>
    internal byte[]? RsaExponent { get; init; }

    internal static bool TryParse(ReadOnlySpan<byte> cbor, [NotNullWhen(true)] out CoseKey? result)
    {
        result = null;

        try
        {
            var reader = new CborReader(cbor.ToArray());
            var initialBytesRemaining = reader.BytesRemaining;

            if (reader.PeekState() != CborReaderState.StartMap)
            {
                return false;
            }

            int? keyType = null;
            int? algorithm = null;
            int? param1Int = null;
            byte[]? param1Bytes = null;
            byte[]? param2Bytes = null;
            byte[]? param3Bytes = null;

            var mapLength = reader.ReadStartMap();

            for (var i = 0; i < mapLength; i++)
            {
                var label = reader.ReadInt32();

                switch (label)
                {
                    case LabelKeyType:
                        keyType = reader.ReadInt32();
                        break;
                    case LabelAlgorithm:
                        algorithm = reader.ReadInt32();
                        break;
                    case LabelParam1:
                        if (reader.PeekState() == CborReaderState.ByteString)
                        {
                            param1Bytes = reader.ReadByteString();
                        }
                        else
                        {
                            param1Int = reader.ReadInt32();
                        }

                        break;
                    case LabelParam2:
                        param2Bytes = reader.ReadByteString();
                        break;
                    case LabelParam3:
                        param3Bytes = reader.ReadByteString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            if (keyType is null || algorithm is null)
            {
                return false;
            }

            var bytesConsumed = initialBytesRemaining - reader.BytesRemaining;
            var rawCbor = cbor[..bytesConsumed].ToArray();

            result = new CoseKey(keyType.Value, algorithm.Value, rawCbor);

            switch (keyType.Value)
            {
                case CoseConstants.KeyTypes.Ec2:
                    result = result with { EcCurve = param1Int, EcX = param2Bytes, EcY = param3Bytes };
                    break;
                case CoseConstants.KeyTypes.Rsa:
                    result = result with { RsaModulus = param1Bytes, RsaExponent = param2Bytes };
                    break;
            }

            return true;
        }
        catch (CborContentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

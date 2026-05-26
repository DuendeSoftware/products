// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Attested credential data from authenticator data.
/// </summary>
internal sealed record AttestedCredentialData(
    Guid Aaguid,
    PasskeyCredentialId CredentialId,
    CoseKey PublicKey)
{
    private const int AaguidLength = 16;
    private const int CredentialIdLengthSize = 2;

    internal static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out AttestedCredentialData? result)
    {
        result = null;

        if (data.Length < AaguidLength + CredentialIdLengthSize)
        {
            return false;
        }

        // AAGUID (16 bytes) - stored as big-endian UUID
        var aaguidBytes = data[..AaguidLength];
        var aaguid = new Guid(aaguidBytes, bigEndian: true);

        // Credential ID length (2 bytes, big-endian)
        var credentialIdLength = BinaryPrimitives.ReadUInt16BigEndian(
            data.Slice(AaguidLength, CredentialIdLengthSize));

        var credentialIdStart = AaguidLength + CredentialIdLengthSize;
        if (data.Length < credentialIdStart + credentialIdLength)
        {
            return false;
        }

        // Credential ID
        if (!PasskeyCredentialId.TryFrom(
                data.Slice(credentialIdStart, credentialIdLength).ToArray(),
                out var credentialId))
        {
            return false;
        }

        // Public key (CBOR)
        var publicKeyStart = credentialIdStart + credentialIdLength;
        var publicKeyData = data[publicKeyStart..];

        if (!CoseKey.TryParse(publicKeyData, out var publicKey))
        {
            return false;
        }

        result = new AttestedCredentialData(aaguid, credentialId.Value, publicKey);
        return true;
    }
}

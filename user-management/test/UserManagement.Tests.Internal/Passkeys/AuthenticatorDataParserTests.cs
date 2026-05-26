// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Formats.Cbor;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public static class AuthenticatorDataTests
{
    private const byte Filler = 0xAA;
    [Fact]
    public static void TryParse_minimal_AuthData_parses_correctly()
    {
        // 32 bytes rpIdHash + 1 byte flags + 4 bytes signCount = 37 bytes minimum
        var authData = new byte[37];
        // Set rpIdHash to all 0xAA
        Array.Fill(authData, Filler, 0, 32);
        // Set flags (User Present)
        authData[32] = 0x01;
        // Set signCount to 42 (big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(authData.AsSpan(33, 4), 42);

        AuthenticatorData.TryParse(authData, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.RpIdHash.ShouldAllBe(b => b == Filler);
        result.RpIdHash.Length.ShouldBe(32);
        result.Flags.ShouldBe(AuthenticatorDataFlags.UserPresent);
        result.SignCount.ShouldBe(42u);
        result.AttestedCredential.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_too_short_returns_false()
    {
        var authData = new byte[36]; // 1 byte short

        AuthenticatorData.TryParse(authData, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_extracts_flags_correctly()
    {
        var authData = new byte[37];
        // Set flags (excluding AT flag since we don't have attested credential data)
        // UserPresent=0x01, UserVerified=0x04, Extension Data=0x80
        authData[32] = 0x01 | 0x04 | 0x80;

        AuthenticatorData.TryParse(authData, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.Flags.HasFlag(AuthenticatorDataFlags.UserPresent).ShouldBeTrue();
        result.Flags.HasFlag(AuthenticatorDataFlags.UserVerified).ShouldBeTrue();
        result.Flags.HasFlag(AuthenticatorDataFlags.AttestedCredentialData).ShouldBeFalse();
        result.Flags.HasFlag(AuthenticatorDataFlags.ExtensionData).ShouldBeTrue();
    }

    [Fact]
    public static void TryParse_extracts_sign_count_big_endian()
    {
        var authData = new byte[37];
        // Set signCount to 0x01020304 (big-endian)
        authData[33] = 0x01;
        authData[34] = 0x02;
        authData[35] = 0x03;
        authData[36] = 0x04;

        AuthenticatorData.TryParse(authData, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.SignCount.ShouldBe(0x01020304u);
    }

    [Fact]
    public static void TryParse_with_attested_credential_extracts_data()
    {
        // Build authData with attested credential data
        var rpIdHash = new byte[32];
        Array.Fill(rpIdHash, Filler);

        var flags = (byte)(AuthenticatorDataFlags.UserPresent | AuthenticatorDataFlags.AttestedCredentialData);
        var signCount = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(signCount, 100);

        // AAGUID (16 bytes)
        var aaguid = Guid.Parse("01020304-0506-0708-090a-0b0c0d0e0f10");
        var aaguidBytes = aaguid.ToByteArray(bigEndian: true);

        // Credential ID length (2 bytes, big-endian) = 4
        var credentialIdLength = new byte[] { 0x00, 0x04 };
        var credentialId = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        // COSE key (minimal - just kty=2, alg=-7)
        var coseKeyWriter = new CborWriter();
        coseKeyWriter.WriteStartMap(2);
        coseKeyWriter.WriteInt32(1); // kty label
        coseKeyWriter.WriteInt32(2); // EC2 key type
        coseKeyWriter.WriteInt32(3); // alg label
        coseKeyWriter.WriteInt32(-7); // ES256
        coseKeyWriter.WriteEndMap();
        var coseKey = coseKeyWriter.Encode();

        // Assemble authData
        var authData = new byte[37 + 16 + 2 + 4 + coseKey.Length];
        var offset = 0;
        rpIdHash.CopyTo(authData, offset);
        offset += 32;
        authData[offset++] = flags;
        signCount.CopyTo(authData, offset);
        offset += 4;
        aaguidBytes.CopyTo(authData, offset);
        offset += 16;
        credentialIdLength.CopyTo(authData, offset);
        offset += 2;
        credentialId.CopyTo(authData, offset);
        offset += 4;
        coseKey.CopyTo(authData, offset);

        AuthenticatorData.TryParse(authData, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        _ = result.AttestedCredential.ShouldNotBeNull();
        result.AttestedCredential.Aaguid.ShouldBe(aaguid);
        result.AttestedCredential.CredentialId.ToBytes().ShouldBe(credentialId);
        _ = result.AttestedCredential.PublicKey.ShouldNotBeNull();
        result.AttestedCredential.PublicKey.KeyType.ShouldBe(2);
        result.AttestedCredential.PublicKey.Algorithm.ShouldBe(-7);
    }

    [Fact]
    public static void TryParse_attested_credential_flag_but_data_too_short_returns_false()
    {
        var authData = new byte[37];
        // Set AT flag but no attested credential data
        authData[32] = (byte)AuthenticatorDataFlags.AttestedCredentialData;

        AuthenticatorData.TryParse(authData, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }
}

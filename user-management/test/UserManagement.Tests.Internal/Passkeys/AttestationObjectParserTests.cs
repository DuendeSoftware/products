// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public static class AttestationObjectParserTests
{
    [Fact]
    public static void TryParse_valid_Cbor_returns_expected_values()
    {
        // Create a valid attestation object CBOR
        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(PasskeyConstants.AttestationFormat.None);
        writer.WriteTextString("authData");
        writer.WriteByteString(new byte[37]); // Minimal authData: 32 (rpIdHash) + 1 (flags) + 4 (signCount)
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteEndMap();
        var cbor = writer.Encode();

        AttestationObject.TryParse(cbor, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.Format.ShouldBe(PasskeyConstants.AttestationFormat.None);
        result.AuthData.Length.ShouldBe(37);
        result.AttStmt.Count.ShouldBe(0);
    }

    [Fact]
    public static void TryParse_malformed_Cbor_returns_false()
    {
        // This is considered invalid because there's no indefinite-length container to close
        // and the rest of the following bytes is garbage data.
        var cbor = new byte[] { 0xFF, 0x1F, 0xFF };

        AttestationObject.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_missing_fmt_returns_false()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(2);
        writer.WriteTextString("authData");
        writer.WriteByteString(new byte[37]);
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteEndMap();
        var cbor = writer.Encode();

        AttestationObject.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_missing_AuthData_returns_false()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(2);
        writer.WriteTextString("fmt");
        writer.WriteTextString(PasskeyConstants.AttestationFormat.None);
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteEndMap();
        var cbor = writer.Encode();

        AttestationObject.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_not_a_map_returns_false()
    {
        var writer = new CborWriter();
        writer.WriteTextString("not a map");
        var cbor = writer.Encode();

        AttestationObject.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_with_AttStmt_values_extracts_correctly()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(PasskeyConstants.AttestationFormat.Packed);
        writer.WriteTextString("authData");
        writer.WriteByteString(new byte[37]);
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(2);
        writer.WriteTextString("alg");
        writer.WriteInt64(CoseAlgorithms.Es256); // ES256
        writer.WriteTextString("sig");
        writer.WriteByteString(new byte[] { 1, 2, 3 });
        writer.WriteEndMap();
        writer.WriteEndMap();
        var cbor = writer.Encode();

        AttestationObject.TryParse(cbor, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.Format.ShouldBe(PasskeyConstants.AttestationFormat.Packed);
        result.AttStmt.Count.ShouldBe(2);
        result.AttStmt["alg"].ShouldBe(CoseAlgorithms.Es256);
        ((byte[])result.AttStmt["sig"]!).ShouldBe([1, 2, 3]);
    }
}

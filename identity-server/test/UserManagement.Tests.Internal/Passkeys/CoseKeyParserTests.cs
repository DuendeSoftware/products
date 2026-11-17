// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public static class CoseKeyParserTests
{
    private const int KeyTypeLabel = 1;
    private const int EllipticCurveKeyType = 2;
    private const int AlgorithmLabel = 3;
    [Fact]
    public static void TryParse_Es256_key_parses_correctly()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(2);
        writer.WriteInt32(KeyTypeLabel);
        writer.WriteInt32(EllipticCurveKeyType);
        writer.WriteInt32(AlgorithmLabel);
        writer.WriteInt32(CoseAlgorithms.Es256);
        writer.WriteEndMap();
        var cbor = writer.Encode();

        CoseKey.TryParse(cbor, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.KeyType.ShouldBe(EllipticCurveKeyType);
        result.Algorithm.ShouldBe(CoseAlgorithms.Es256);
    }

    [Fact]
    public static void TryParse_Rs256_key_parses_correctly()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(2);
        writer.WriteInt32(KeyTypeLabel);
        writer.WriteInt32(EllipticCurveKeyType);
        writer.WriteInt32(AlgorithmLabel);
        writer.WriteInt32(CoseAlgorithms.Rs256);
        writer.WriteEndMap();
        var cbor = writer.Encode();

        CoseKey.TryParse(cbor, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.KeyType.ShouldBe(EllipticCurveKeyType);
        result.Algorithm.ShouldBe(CoseAlgorithms.Rs256);
    }

    [Fact]
    public static void TryParse_malformed_Cbor_returns_false()
    {
        var cbor = new byte[] { 0xFF, 0xFF, 0xFF };

        CoseKey.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_not_a_map_returns_false()
    {
        var writer = new CborWriter();
        writer.WriteTextString("not a map");
        var cbor = writer.Encode();

        CoseKey.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_missing_kty_returns_false()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(1);
        writer.WriteInt32(AlgorithmLabel);
        writer.WriteInt32(CoseAlgorithms.Es256);
        writer.WriteEndMap();
        var cbor = writer.Encode();

        CoseKey.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_missing_alg_returns_false()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(1);
        writer.WriteInt32(KeyTypeLabel);
        writer.WriteInt32(EllipticCurveKeyType);
        writer.WriteEndMap();
        var cbor = writer.Encode();

        CoseKey.TryParse(cbor, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_preserves_raw_Cbor()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteInt32(KeyTypeLabel);
        writer.WriteInt32(EllipticCurveKeyType);
        writer.WriteInt32(AlgorithmLabel);
        writer.WriteInt32(CoseAlgorithms.Es256);
        writer.WriteInt32(-1); // x coordinate (EC2)
        writer.WriteByteString(new byte[32]);
        writer.WriteEndMap();
        var cbor = writer.Encode();

        CoseKey.TryParse(cbor, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.RawCbor.ShouldBe(cbor);
    }

    [Fact]
    public static void TryParse_with_extra_data_after_only_consumes_key()
    {
        var writer = new CborWriter();
        writer.WriteStartMap(2);
        writer.WriteInt32(KeyTypeLabel);
        writer.WriteInt32(EllipticCurveKeyType);
        writer.WriteInt32(AlgorithmLabel);
        writer.WriteInt32(CoseAlgorithms.Es256);
        writer.WriteEndMap();
        var keyBytes = writer.Encode();

        // Add extra garbage after the key
        var cbor = new byte[keyBytes.Length + 10];
        keyBytes.CopyTo(cbor, 0);
        Array.Fill(cbor, (byte)0xFF, keyBytes.Length, 10);

        CoseKey.TryParse(cbor, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.RawCbor.Count.ShouldBe(keyBytes.Length);
    }
}

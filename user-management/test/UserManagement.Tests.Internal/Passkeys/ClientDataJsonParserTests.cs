// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public static class ClientDataJsonTests
{
    [Fact]
    public static void TryParse_valid_ClientData_returns_expected_values()
    {
        var json = """{"type":"webauthn.create","challenge":"dGVzdC1jaGFsbGVuZ2U","origin":"https://example.com"}"""u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.Type.ShouldBe("webauthn.create");
        result.Challenge.ShouldBe("dGVzdC1jaGFsbGVuZ2U");
        result.Origin.ShouldBe("https://example.com");
        result.CrossOrigin.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_with_cross_origin_parses_correctly()
    {
        var json = """{"type":"webauthn.get","challenge":"abc123","origin":"https://example.com","crossOrigin":true}"""u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeTrue();

        _ = result.ShouldNotBeNull();
        result.CrossOrigin.ShouldBe(true);
    }

    [Fact]
    public static void TryParse_malformed_json_returns_false()
    {
        var json = "not valid json"u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_missing_type_returns_false()
    {
        var json = """{"challenge":"abc123","origin":"https://example.com"}"""u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_missing_challenge_returns_false()
    {
        var json = """{"type":"webauthn.create","origin":"https://example.com"}"""u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_missing_origin_returns_false()
    {
        var json = """{"type":"webauthn.create","challenge":"abc123"}"""u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_empty_object_returns_false()
    {
        var json = "{}"u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }

    [Fact]
    public static void TryParse_null_values_returns_false()
    {
        var json = """{"type":null,"challenge":"abc","origin":"https://example.com"}"""u8;

        ClientDataJson.TryParse(json, out var result).ShouldBeFalse();

        result.ShouldBeNull();
    }
}

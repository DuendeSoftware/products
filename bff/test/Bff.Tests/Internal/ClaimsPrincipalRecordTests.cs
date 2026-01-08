// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.Bff.Internal;

namespace Duende.Bff.Tests.Internal;

public class ClaimsPrincipalRecordTests
{
    [Fact]
    public void Can_convert_between_ClaimsPrincipal_and_ClaimsPrincipalRecord()
    {
        var original = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "123"),
            new Claim("name", "Alice"),
            new Claim("role", "admin")
        }, "TestAuthType", "name", "role"));

        var record = original.ToClaimsPrincipalLite();
        var reconstructed = record.ToClaimsPrincipal();

        reconstructed.Identity!.AuthenticationType.ShouldBe(original.Identity!.AuthenticationType);
        reconstructed.Identity!.Name.ShouldBe(original.Identity!.Name);

        var originalClaims = original.Claims.ToDictionary(c => c.Type, c => c.Value);
        var reconstructedClaims = reconstructed.Claims.ToDictionary(c => c.Type, c => c.Value);

        originalClaims.Count.ShouldBe(reconstructedClaims.Count);
        foreach (var kvp in originalClaims)
        {
            reconstructedClaims.ShouldContainKey(kvp.Key);
            reconstructedClaims[kvp.Key].ShouldBe(kvp.Value);
        }
    }

    [Fact]
    public void Can_convert_default_ClaimsPrincipalRecord()
    {
        var original = new ClaimsPrincipalRecord();

        Should.NotThrow(() => original.ToClaimsPrincipal());
    }

    [Fact]
    public void Can_convert_ClaimsPrincipalRecord_with_default_ClaimsRecord()
    {
        var original = new ClaimsPrincipalRecord
        {
            Claims =
            [
                new ClaimRecord()
            ]
        };

        Should.NotThrow(() => original.ToClaimsPrincipal());
    }

    [Fact]
    public void ToClaimsPrincipal_handles_null_Claims_from_deserialization()
    {
        var serializedClaimsPrincipal = """{"Claims": null}""";
        var record = JsonSerializer.Deserialize<ClaimsPrincipalRecord>(serializedClaimsPrincipal);

        Should.NotThrow(() => record!.ToClaimsPrincipal());
    }

    [Fact]
    public void ToClaimsPrincipal_handles_null_Type_in_ClaimRecord_from_deserialization()
    {
        var serializedClaimsPrincipal = """{"Claims": [{"type": null, "value": "test"}]}""";
        var record = JsonSerializer.Deserialize<ClaimsPrincipalRecord>(serializedClaimsPrincipal);

        var principal = record!.ToClaimsPrincipal();
        principal.Claims.Single().Type.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToClaimsPrincipal_handles_null_Value_in_ClaimRecord_from_deserialization()
    {
        var serializedClaimsPrincipal = """{"Claims": [{"type": "sub", "value": null}]}""";
        var record = JsonSerializer.Deserialize<ClaimsPrincipalRecord>(serializedClaimsPrincipal);

        var principal = record!.ToClaimsPrincipal();
        principal.Claims.Single().Value.ShouldBe(string.Empty);
    }
}

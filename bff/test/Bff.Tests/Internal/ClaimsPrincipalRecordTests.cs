// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Internal;
using System.Security.Claims;

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
            Claims = [
                new ClaimRecord()
            ]
        };

        Should.NotThrow(() => original.ToClaimsPrincipal());
    }
}

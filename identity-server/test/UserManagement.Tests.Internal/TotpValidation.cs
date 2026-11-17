// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Totp.Internal;

namespace Duende.Platform.UserManagement;

public static class TotpValidation
{
    // GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ in Base32
    private static readonly byte[] Key = "12345678901234567890"u8.ToArray();

    // https://datatracker.ietf.org/doc/html/rfc6238#appendix-B
    [Theory]
    [InlineData(59UL, "94287082")] // 1970
    [InlineData(1111111109UL, "07081804")] // 2005
    [InlineData(1111111111UL, "14050471")] // 2005 next 30-second window
    [InlineData(1234567890UL, "89005924")] // 2009
    [InlineData(2000000000UL, "69279037")] // 2033
    [InlineData(20000000000UL, "65353130")] // 2603
    // additional
    [InlineData(946684800UL, "52795445")] // 2000-01-01 (TimeProvider zero)
    [InlineData(1111111051UL, "89731029")] // 2005 minus 60 seconds
    [InlineData(1111111081UL, "07081804")] // 2005 minus 30 seconds
    [InlineData(1111111141UL, "44266759")] // 2005 plus 30 seconds
    [InlineData(1111111171UL, "02306183")] // 2005 plus 60 seconds
    public static void SatisfiesRfc6238TestVectors(ulong unixTimeSeconds, string totp)
    {
        var validated = Totp.Validate(Key, (byte)totp.Length, unixTimeSeconds, totp, null, out _);

        validated.ShouldBeTrue();
    }
}

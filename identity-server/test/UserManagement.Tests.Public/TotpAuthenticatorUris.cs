// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Totp;

namespace Duende.Platform.UserManagement;

public static class TotpAuthenticatorUris
{
    [Fact]
    public static void Generates_correct_uri_format()
    {
        var key = PlainBytesTotpKey.DecodeFromBase32("JBSWY3DPEHPK3PXP");

        var uri = TotpAuthenticatorUri.Generate("MyApp", "user@example.com", key);

        uri.ShouldBe("otpauth://totp/MyApp:user%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=MyApp&digits=6");
    }

    [Fact]
    public static void Encodes_issuer_and_account()
    {
        var key = PlainBytesTotpKey.DecodeFromBase32("JBSWY3DPEHPK3PXP");

        var uri = TotpAuthenticatorUri.Generate("My App & Co.", "user+test@example.com", key);

        uri.ShouldBe("otpauth://totp/My%20App%20%26%20Co.:user%2Btest%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=My%20App%20%26%20Co.&digits=6");
    }

    [Fact]
    public static void Throws_when_issuer_is_null()
    {
        var key = PlainBytesTotpKey.DecodeFromBase32("JBSWY3DPEHPK3PXP");

        var ex = Record.Exception(() =>
            TotpAuthenticatorUri.Generate(null!, "user@example.com", key));

        _ = ex.ShouldNotBeNull();
    }

    [Fact]
    public static void Throws_when_issuer_is_white_space()
    {
        var key = PlainBytesTotpKey.DecodeFromBase32("JBSWY3DPEHPK3PXP");

        var ex = Record.Exception(() =>
            TotpAuthenticatorUri.Generate("   ", "user@example.com", key));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public static void Throws_when_account_is_null()
    {
        var key = PlainBytesTotpKey.DecodeFromBase32("JBSWY3DPEHPK3PXP");

        var ex = Record.Exception(() => TotpAuthenticatorUri.Generate("MyApp", null!, key));

        _ = ex.ShouldNotBeNull();
    }

    [Fact]
    public static void Throws_when_account_is_white_space()
    {
        var key = PlainBytesTotpKey.DecodeFromBase32("JBSWY3DPEHPK3PXP");

        var ex = Record.Exception(() => TotpAuthenticatorUri.Generate("MyApp", "   ", key));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }
}

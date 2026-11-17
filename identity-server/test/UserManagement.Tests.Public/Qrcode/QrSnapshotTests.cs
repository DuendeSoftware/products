// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.QrCodes;

namespace Duende.Platform.UserManagement.Qrcode;

public static class QrSnapshotTests
{
    // Short numeric string — exercises numeric mode, produces a small version symbol.
    [Fact]
    public static Task Short_numeric_string()
    {
        var symbol = QrEncoder.Encode("8675309");
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg).UseDirectory("Snapshots");
    }

    // Classic ISO 18004 Annex I reference input — alphanumeric mode.
    [Fact]
    public static Task Alphanumeric_string()
    {
        var symbol = QrEncoder.Encode("HELLO WORLD");
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg).UseDirectory("Snapshots");
    }

    // Realistic TOTP URI — byte mode, the primary use case for this encoder.
    [Fact]
    public static Task TotpUri()
    {
        var uri = "otpauth://totp/Example:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Example";
        var symbol = QrEncoder.Encode(uri, new QrEncodeOptions { EccLevel = QrEccLevel.M });
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg).UseDirectory("Snapshots");
    }

    // Long data that cannot fit in version 1 — forces automatic version selection upward.
    [Fact]
    public static Task Long_data_forces_higher_version()
    {
        var data = new string('A', 200);
        var symbol = QrEncoder.Encode(data);
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg).UseDirectory("Snapshots");
    }

    // Each ECC level must produce a distinct output for the same input.
    [Theory]
    [InlineData(QrEccLevel.L)]
    [InlineData(QrEccLevel.M)]
    [InlineData(QrEccLevel.Q)]
    [InlineData(QrEccLevel.H)]
    public static Task EccLevelsProduceDifferentOutputs(QrEccLevel ecc)
    {
        var symbol = QrEncoder.Encode("HELLO WORLD", new QrEncodeOptions { EccLevel = ecc });
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg)
            .UseDirectory("Snapshots")
            .UseParameters(ecc);
    }

    // Explicit version override — encoder must honour the requested version rather than auto-select.
    [Fact]
    public static Task Forced_version()
    {
        var symbol = QrEncoder.Encode("HELLO WORLD", new QrEncodeOptions { Version = 5, EccLevel = QrEccLevel.Q });
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg).UseDirectory("Snapshots");
    }

    // Long numeric string — exercises multi-block interleaving in numeric mode.
    [Fact]
    public static Task Numeric_mode_large_input()
    {
        var data = new string('1', 60);
        var symbol = QrEncoder.Encode(data);
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg).UseDirectory("Snapshots");
    }

    // Non-ASCII UTF-8 characters — exercises byte mode with multi-byte sequences.
    [Fact]
    public static Task Byte_mode_utf8()
    {
        var symbol = QrEncoder.Encode("こんにちは", new QrEncodeOptions { EccLevel = QrEccLevel.M });
        var svg = QrSvgRenderer.Render(symbol);
        return Verify(svg).UseDirectory("Snapshots");
    }

    // PNG renderer — TOTP URI rendered as PNG, snapshotted as base64 for text-diffable verification.
    [Fact]
    public static Task Png_TotpUri()
    {
        var uri = "otpauth://totp/Example:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Example";
        var symbol = QrEncoder.Encode(uri, new QrEncodeOptions { EccLevel = QrEccLevel.M });
        var png = QrPngRenderer.Render(symbol);
        var base64 = Convert.ToBase64String(png);
        return Verify(base64).UseDirectory("Snapshots");
    }

    // PNG renderer with scaled modules — exercises moduleSize parameter.
    [Fact]
    public static Task Png_scaled_modules()
    {
        var symbol = QrEncoder.Encode("HELLO WORLD");
        var png = QrPngRenderer.Render(symbol, moduleSize: 4);
        var base64 = Convert.ToBase64String(png);
        return Verify(base64).UseDirectory("Snapshots");
    }
}

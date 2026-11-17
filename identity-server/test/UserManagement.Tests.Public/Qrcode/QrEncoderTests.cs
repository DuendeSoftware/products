// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.QrCodes;

namespace Duende.Platform.UserManagement.Qrcode;

public static class QrEncoderTests
{
    // Empty input has no data to encode - the encoder must reject it early.
    [Fact]
    public static void Encode_empty_string_throws() =>
        Should.Throw<ArgumentException>(() => QrEncoder.Encode(""));

    // Null input must be caught by the guard clause, not fail deep in the pipeline.
    [Fact]
    public static void Encode_null_string_throws() =>
        Should.Throw<ArgumentException>(() => QrEncoder.Encode((string)null!));

    [Fact]
    public static void Encode_forced_version_too_small_throws()
    {
        // 100 bytes of data cannot fit in version 1 at any ECC level.
        var bigData = new string('A', 100);
        var options = new QrEncodeOptions { Version = 1, EccLevel = QrEccLevel.L };

        _ = Should.Throw<InvalidOperationException>(() => QrEncoder.Encode(bigData, options));
    }
}

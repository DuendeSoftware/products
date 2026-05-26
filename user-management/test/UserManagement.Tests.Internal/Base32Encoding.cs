// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.UserManagement.Authentication.Totp.Internal;

namespace Duende.Platform.UserManagement;

public static class Base32Encoding
{
    // https://datatracker.ietf.org/doc/html/rfc4648#section-10
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY======")]
    [InlineData("fo", "MZXQ====")]
    [InlineData("foo", "MZXW6===")]
    [InlineData("foob", "MZXW6YQ=")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI======")]
    public static void EncodingSatisfiesRfc4648TestVectors(string ascii, string expectedCode)
    {
        var bytes = Encoding.ASCII.GetBytes(ascii);

        var actualCode = Base32.Encode(bytes);

        actualCode.ShouldBe(expectedCode);
    }

    // https://datatracker.ietf.org/doc/html/rfc4648#section-10
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY======")]
    [InlineData("fo", "MZXQ====")]
    [InlineData("foo", "MZXW6===")]
    [InlineData("foob", "MZXW6YQ=")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI======")]
    public static void DecodingSatisfiesRfc4648TestVectors(string expectedAscii, string base32)
    {
        var expectedBytes = Encoding.ASCII.GetBytes(expectedAscii);

        var decoded = Base32.TryDecode(base32, out var actualBytes);

        decoded.ShouldBeTrue();
        actualBytes.ShouldBe(expectedBytes);
    }
}

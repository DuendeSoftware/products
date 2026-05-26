// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.QrCodes.Internal;

namespace Duende.Platform.UserManagement.QrCode;

public static class ReedSolomonTests
{
    [Fact]
    public static void Gf256_exp_log_round_trip()
    {
        for (var x = 1; x <= 255; x++)
        {
            Gf256.Exp[Gf256.Log[x]].ShouldBe((byte)x);
        }
    }

    [Fact]
    public static void Gf256_multiply_by_zero_returns_zero()
    {
        Gf256.Multiply(0, 42).ShouldBe((byte)0);
        Gf256.Multiply(42, 0).ShouldBe((byte)0);
    }

    [Fact]
    public static void Gf256_multiply_one_by_one_returns_one() =>
        Gf256.Multiply(1, 1).ShouldBe((byte)1);

    [Theory]
    [InlineData(3, 7)]
    [InlineData(100, 200)]
    [InlineData(0x53, 0xCA)]
    public static void Gf256MultiplyIsCommutative(byte a, byte b) =>
        Gf256.Multiply(a, b).ShouldBe(Gf256.Multiply(b, a));

    [Fact]
    public static void Generator_polynomial_length()
    {
        var gen = ReedSolomon.GetGeneratorPolynomial(10);

        gen.Length.ShouldBe(11);
        gen[0].ShouldBe((byte)1);
    }

    [Fact]
    public static void Generator_polynomial_degree2()
    {
        // g(x) = (x + a^0)(x + a^1) = x^2 + (1 XOR a)x + a = x^2 + 3x + 2
        var gen = ReedSolomon.GetGeneratorPolynomial(2);

        gen.ShouldBe(new byte[] { 1, 3, 2 });
    }

    [Fact]
    public static void Compute_ecc_known_vector()
    {
        // ISO 18004 Annex I: "HELLO WORLD" version 1-M encoded data codewords
        byte[] data = [32, 91, 11, 120, 209, 114, 220, 77, 67, 64, 236, 17, 236, 17, 236, 17];
        byte[] expectedEcc = [196, 35, 39, 119, 235, 215, 231, 226, 93, 23];

        var ecc = ReedSolomon.ComputeEcc(data, 10);

        ecc.ShouldBe(expectedEcc);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(22)]
    public static void ComputeEccOutputLength(int eccCount)
    {
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8];

        var ecc = ReedSolomon.ComputeEcc(data, eccCount);

        ecc.Length.ShouldBe(eccCount);
    }

    [Fact]
    public static void Generator_polynomial_is_memoized()
    {
        var first = ReedSolomon.GetGeneratorPolynomial(15);
        var second = ReedSolomon.GetGeneratorPolynomial(15);

        ReferenceEquals(first, second).ShouldBeTrue();
    }
}

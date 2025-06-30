// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;

namespace IdentityServer.UnitTests.Configuration;

public class CryptoHelperTests
{
    [Theory]
    [InlineData("SHA256", 256)]
    [InlineData("SHA512", 512)]
    [InlineData("SHA384", 384)]
    [InlineData("    SHA256", 256)]
    [InlineData("    SHA256      ", 256)]
    [InlineData("SSHA256", 256)]
    [InlineData("SSHASSSS256", 256)]
    [InlineData("AES256", 256)]
    [InlineData("JOE256", 256)]
    [InlineData("256", 256)]
    [InlineData("384", 384)]
    [InlineData("512", 512)]
    public void Can_GetHashFunctionForSigningAlgorithm_From_SigningAlgorithm(string algorithm, int hashLength)
    {
        var (hash, length) = CryptoHelper.GetHashFunctionForSigningAlgorithm(algorithm);

        length.ShouldBe(hashLength);
        hash("test"u8.ToArray()).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("SHA1")]
    [InlineData("SHA1000")]
    [InlineData("SHA666")]
    [InlineData("BAAAAD")]
    [InlineData("")]
    [InlineData("1")]
    public void Can_GetHashFunctionForSigningAlgorithm_From_SigningAlgorithm_With_Invalid_Algorithm(string algorithm)
        => Should.Throw<InvalidOperationException>(() => CryptoHelper.GetHashFunctionForSigningAlgorithm(algorithm));
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Duende.IdentityServer.Configuration;

namespace IdentityServer.UnitTests.Configuration;

public class CryptoHelperTests
{
    [Theory]
    [InlineData("SHA256", 256)]
    [InlineData("SHA512", 512)]
    [InlineData("SHA384", 384)]
    public void Can_get_crypto_hash_function(string algorithm, int length)
    {
        var target = CryptoHelper.GetHashFunctionForSigningAlgorithm;

        var (func, hashLength) = target(algorithm);
        Func<byte[], byte[]> expectedHash = hashLength switch
        {
            256 => SHA256.HashData,
            384 => SHA384.HashData,
            512 => SHA512.HashData,
            _ => throw new Exception("Invalid hash length")
        };

        hashLength.ShouldBeEquivalentTo(length);
        func.ShouldBeEquivalentTo(expectedHash);
    }
}
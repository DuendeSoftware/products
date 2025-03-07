// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Duende.IdentityServer.Configuration;

namespace IdentityServer.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory(["GetHashFunctionForSigningAlgorithm"])]
public class GetHashFunctionForSigningAlgorithmBenchmark
{
    private static string Hash =>
        new[] { "SHA256", "SHA384", "SHA512" }[Random.Shared.Next(0, 3)];

    [Benchmark]
    public void GetHashFunctionForSigningAlgorithm() =>
        CryptoHelper.GetHashFunctionForSigningAlgorithm(Hash);
}
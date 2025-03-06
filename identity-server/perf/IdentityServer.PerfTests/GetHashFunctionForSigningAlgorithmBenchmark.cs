// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using BenchmarkDotNet.Attributes;
using Duende.IdentityServer.Configuration;

namespace IdentityServer.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory(["GetHashFunctionForSigningAlgorithm"])]
public class GetHashFunctionForSigningAlgorithmBenchmark
{
    private static string Hash =>
        new[] { "RS256", "RS384", "RS512", "ES256", "ES384", "ES512" }[Random.Shared.Next(0, 6)];

    [Benchmark(Baseline = true, Description = "Non-optimized")]
    public void GetHashFunctionForSigningAlgorithm() =>
        CryptoHelper.GetHashFunctionForSigningAlgorithm(Hash);

    [Benchmark(Baseline = false, Description = "Optimized")]
    public void GetHashFunctionSigningAlgorithmOptimized() =>
        CryptoHelper.GetHashFunctionForSigningAlgorithmOptimized(Hash);
}
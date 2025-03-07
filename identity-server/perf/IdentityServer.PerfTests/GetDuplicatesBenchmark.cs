// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq;
using BenchmarkDotNet.Attributes;
using Duende.IdentityServer.Stores;

namespace IdentityServer.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("GetDuplicates")]
public class GetDuplicatesBenchmark
{
    private static readonly string[] Data = ["Maarten", "Khalid", "Khalid", "Joe", "Andrea", "Gina", "Joe", "Scott", "Tyler", "Damian", "Damian"];

    [Benchmark]
    public void GetDuplicates()
        => _ = IResourceStoreExtensions.GetDuplicates(Data).ToArray();
}
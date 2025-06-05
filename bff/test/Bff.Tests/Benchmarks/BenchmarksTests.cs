// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.Benchmarks;

namespace Duende.Bff.Tests.Benchmarks;

public class BenchmarksTests(ProxyBenchmarksFixture benchmarks) : IClassFixture<ProxyBenchmarksFixture>
{
    [Fact]
    public async Task RunBffBenchmark() => await benchmarks.ViaBff();
}

public class ProxyBenchmarksFixture : ProxyBenchmarks, IAsyncLifetime
{
    public async Task InitializeAsync() => await Start();
}

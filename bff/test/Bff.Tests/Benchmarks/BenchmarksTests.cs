// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.Benchmarks;

namespace Duende.Bff.Tests.Benchmarks;

public class BenchmarksTests(ProxyBenchmarksFixture benchmarks) : IClassFixture<ProxyBenchmarksFixture>
{
    [Fact]
    public async Task BffUserToken() =>
        await benchmarks.BffUserToken();

    [Fact]
    public async Task YarpProxy() =>
        await benchmarks.YarpProxy();

    [Fact]
    public async Task DirectToApi() =>
        await benchmarks.DirectToApi();

    [Fact]
    public async Task BffClientCredentialsToken() =>
        await benchmarks.BffClientCredentialsToken();

    [Fact]
    public async Task BffWithManyFrontends() =>
        await benchmarks.BffWithManyFrontends();
}

public class ProxyBenchmarksFixture : ProxyBenchmarks, IAsyncLifetime
{
    public async Task InitializeAsync() => await Start();
}

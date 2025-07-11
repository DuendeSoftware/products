// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

public class YarpBenchmarks : BenchmarkBase
{
    private YarpFixture _fixture = null!;
    private HttpClient _yarpHttpClient = null!;
    private HttpClient _directHttpClient = null!;
    [GlobalSetup]
    public override Task InitializeAsync()
    {
        _fixture = new YarpFixture();
        _directHttpClient = _fixture.Internet.BuildHttpClient(_fixture.Api.Url());
        _yarpHttpClient = _fixture.Internet.BuildHttpClient(_fixture.YarpProxy.Url());
        return Task.CompletedTask;
    }

    [Benchmark]
    public async Task DirectToApi() => await _directHttpClient.GetAsync("/")
        .EnsureStatusCode();

    [Benchmark]
    public async Task YarpProxy() => await _yarpHttpClient.GetAsync("/yarp/test")
        .EnsureStatusCode();
    [GlobalCleanup]
    public override async Task DisposeAsync() => await _fixture.DisposeAsync();

}

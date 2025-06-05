// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

public class ProxyBenchmarks : BenchmarkBase
{
    private ProxyFixture _fixture = null!;

    private HttpClient _authenticatedBffClient = null!;
    private HttpClient _directHttpClient = null!;
    private HttpClient _yarpHttpClient = null!;


    [GlobalSetup]
    public async Task Start()
    {
        _fixture = new ProxyFixture();

        _authenticatedBffClient = new HttpClient()
        {
            BaseAddress = _fixture.Bff.Url
        };
        var loginResult = await _authenticatedBffClient.GetAsync("/bff/login");
        loginResult.EnsureSuccessStatusCode();

        _directHttpClient = new HttpClient
        {
            BaseAddress = _fixture.Api.Url
        };

        _yarpHttpClient = new HttpClient
        {
            BaseAddress = _fixture.YarpProxy.Url
        };
    }


    [Benchmark]
    public async Task DirectToApi()
    {
        var result = await _directHttpClient.GetAsync("/");
        result.EnsureSuccessStatusCode();
    }


    [Benchmark]
    public async Task ViaProxy()
    {
        var result = await _yarpHttpClient.GetAsync("/yarp/test");
        result.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task ViaBff()
    {
        var getToken = await _authenticatedBffClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/user_token")
        {
            Headers =
            {
                {"x-csrf", "1"}
            }
        });
        getToken.EnsureSuccessStatusCode();
    }


    [GlobalCleanup]
    public async Task Stop() => await _fixture.DisposeAsync();

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
        _authenticatedBffClient.Dispose();
        _directHttpClient.Dispose();
        _yarpHttpClient.Dispose();
    }
}

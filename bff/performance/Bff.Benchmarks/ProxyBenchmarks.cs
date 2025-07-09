// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

public class ProxyBenchmarks : BenchmarkBase
{
    private ProxyFixture _fixture = null!;

    private HttpClient _authenticatedBffClient = null!;
    private HttpClient _anonBffClient = null!;
    private HttpClient _bffServerSideSessionsClient = null!;


    [GlobalSetup]
    public override async Task InitializeAsync()
    {
        _fixture = new ProxyFixture();

        _authenticatedBffClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
        await _authenticatedBffClient.GetAsync("/bff/login")
            .EnsureStatusCode();

        _anonBffClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        _bffServerSideSessionsClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        await _bffServerSideSessionsClient.GetAsync("/bff/login")
            .EnsureStatusCode();

    }



    [Benchmark]
    public async Task BffUserToken() => await _authenticatedBffClient
            .GetWithCSRF("/user_token")
            .EnsureStatusCode();

    [Benchmark]
    public async Task BffClientCredentialsToken() => await _authenticatedBffClient
            .GetWithCSRF("/client_token")
            .EnsureStatusCode();

    [Benchmark]
    public async Task BffAnonEndpoint() => await _anonBffClient
        .GetAsync("/anon")
        .EnsureStatusCode();

    [Benchmark]
    public async Task BffLocalEndpoint() => await _authenticatedBffClient
        .GetAsync("/anon")
        .EnsureStatusCode();

    [Benchmark]
    public async Task BffLogin()
    {
        var client = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
        await client.GetAsync("/bff/login")
            .EnsureStatusCode();

        await client.GetWithCSRF("/user_token")
            .EnsureStatusCode();

    }


    //[Benchmark]
    //public async Task BffSSSLocalEndpoint() => await _bffServerSideSessionsClient
    //    .GetAsync("/anon")
    //    .EnsureStatusCode();


    [GlobalCleanup]
    public override async Task DisposeAsync() => await _fixture.DisposeAsync();
}

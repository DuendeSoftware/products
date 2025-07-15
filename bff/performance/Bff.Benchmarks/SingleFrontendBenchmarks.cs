// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

public class SingleFrontendBenchmarks : BenchmarkBase
{
    private SingleFrontendFixture _fixture = null!;

    private HttpClient _authenticatedBffClient = null!;
    private HttpClient _anonBffClient = null!;
    private HttpClient _bffServerSideSessionsClient = null!;


    [GlobalSetup]
    public override async Task InitializeAsync()
    {
        _fixture = new SingleFrontendFixture();

        _authenticatedBffClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
        await _authenticatedBffClient.GetAsync("/bff/login")
            .EnsureStatusCode();

        _anonBffClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        _bffServerSideSessionsClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        await _bffServerSideSessionsClient.GetAsync("/bff/login")
            .EnsureStatusCode();

    }

    [Benchmark]
    public async Task SingleFrontend_UserToken() => await _authenticatedBffClient
            .GetWithCSRF("/user_token")
            .EnsureStatusCode();

    [Benchmark]
    public async Task SingleFrontend_ClientCredentialsToken() => await _authenticatedBffClient
            .GetWithCSRF("/client_token")
            .EnsureStatusCode();

    [Benchmark]
    public async Task SingleFrontend_AnonLocalEndpoint() => await _anonBffClient
        .GetAsync("/anon")
        .EnsureStatusCode();

    [Benchmark]
    public async Task SingleFrontend_LocalEndpoint() => await _authenticatedBffClient
        .GetAsync("/anon")
        .EnsureStatusCode();

    [Benchmark]
    public async Task SingleFrontend_Login()
    {
        var client = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
        await client.GetAsync("/bff/login")
            .EnsureStatusCode();
    }


    //[Benchmark]
    //public async Task BffSSSLocalEndpoint() => await _bffServerSideSessionsClient
    //    .GetAsync("/anon")
    //    .EnsureStatusCode();


    [GlobalCleanup]
    public override async Task DisposeAsync() => await _fixture.DisposeAsync();
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

public class MultiFrontendBenchmarks : BenchmarkBase
{
    private MultiFrontendLoginFixture _fixture = null!;
    private HttpClient _authenticatedBffClient = null!;
    private HttpClient _anonBffClient = null!;

    [GlobalSetup]
    public override async Task InitializeAsync()
    {
        _fixture = new MultiFrontendLoginFixture();
        _authenticatedBffClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        // Warm up the BFF Login
        await _authenticatedBffClient.GetAsync("/bff/login")
            .EnsureStatusCode();

        _anonBffClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
    }

    [Benchmark]
    public async Task MultiFrontend_login_to_default()
    {
        var client = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
        await client.GetAsync("/bff/login")
            .EnsureStatusCode();
    }

    [Benchmark]
    public async Task MultiFrontend_login_to_frontend_3()
    {
        var client = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
        await client.GetAsync($"{GetPath(3)}/bff/login")
            .EnsureStatusCode();
    }

    [Benchmark]
    public async Task MultiFrontend_user_token() =>
        await _authenticatedBffClient.GetWithCSRF("/user_token")
            .EnsureStatusCode();


    [Benchmark]
    public async Task MultiFrontend_ClientCredentialsToken() =>
        await _authenticatedBffClient
        .GetWithCSRF("/client_token")
        .EnsureStatusCode();

    [Benchmark]
    public async Task MultiFrontend_AnonLocalEndpoint() =>
        await _anonBffClient
        .GetAsync("/anon")
        .EnsureStatusCode();

    [Benchmark]
    public async Task MultiFrontend_LocalEndpoint() =>
        await _authenticatedBffClient
        .GetAsync("/anon")
        .EnsureStatusCode();

    public override async Task DisposeAsync() =>
        await _fixture.DisposeAsync();
}

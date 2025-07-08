// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

public class LoginBenchmarks : BenchmarkBase
{
    private MultiFrontendLoginFixture _fixture = null!;
    private const int PathsToTest = 30;

    [GlobalSetup]
    public override async Task InitializeAsync()
    {
        _fixture = new MultiFrontendLoginFixture();
        var bffClient = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        // Warm up the BFF Login
        await bffClient.GetAsync("/bff/login")
            .EnsureStatusCode();

        // Warm up each frontend
        for (var i = 0; i < PathsToTest; i++)
        {
            var c = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
            var path = "/path" + (i + 100);
            await c.GetAsync(path + "/bff/login")
                .EnsureStatusCode();
        }

    }

    [Benchmark]
    public async Task BffLogin_single_frontend()
    {
        var client = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
        await client.GetAsync("/bff/login")
            .EnsureStatusCode();

        await client.GetWithCSRF("/user_token")
            .EnsureStatusCode();

    }


    private static Random _random = new Random();
    [Benchmark]
    public async Task BffLogin_multi_frontend()
    {
        var client = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        var path = "/path" + _random.Next(100, PathsToTest + 100 - 1);

        await client.GetAsync(path + "/bff/login")
            .EnsureStatusCode();

        await client.GetWithCSRF(path + "/user_token")
            .EnsureStatusCode();

    }

    public override async Task DisposeAsync() => await _fixture.DisposeAsync();
}

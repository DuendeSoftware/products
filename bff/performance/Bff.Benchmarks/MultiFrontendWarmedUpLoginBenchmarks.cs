// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

/// <summary>
/// This test warms up all frontends by calling the login endpoint on each one.
/// This means they are present in the cache. Just to make sure that, having many
/// frontends active doesn't cause issues. 
/// </summary>
public class MultiFrontendWarmedUpLoginBenchmarks : BenchmarkBase
{
    private MultiFrontendLoginFixture _fixture = null!;
    private const int PathsToTest = 499;

    [GlobalSetup]
    public override async Task InitializeAsync()
    {
        _fixture = new MultiFrontendLoginFixture();

        // Warm up each frontend
        for (var i = 0; i < PathsToTest; i++)
        {
            var c = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());
            var path = GetPath(i);
            await c.GetAsync(path + "/bff/login")
                .EnsureStatusCode();
        }

    }

    private static Random _random = new Random();
    [Benchmark]
    public async Task Random_frontend_with_500_pre_warmed()
    {
        var client = _fixture.Internet.BuildHttpClient(_fixture.Bff.Url());

        var path = GetPath(_random.Next(0, PathsToTest - 1));

        await client.GetAsync(path + "/bff/login")
            .EnsureStatusCode();

        await client.GetWithCSRF(path + "/user_token")
            .EnsureStatusCode();

    }

    public override async Task DisposeAsync() => await _fixture.DisposeAsync();
}

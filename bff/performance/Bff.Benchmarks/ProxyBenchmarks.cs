// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using BenchmarkDotNet.Attributes;

namespace Bff.Benchmarks;

public class ProxyBenchmarks : BenchmarkBase
{
    private ProxyFixture _fixture = null!;

    private HttpClient _authenticatedBffClient = null!;
    private HttpClient _manyFrontendsBffClient = null!;
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
        await _authenticatedBffClient.GetAsync("/bff/login")
            .EnsureStatusCode();

        _manyFrontendsBffClient = new HttpClient()
        {
            BaseAddress = _fixture.BffWithManyFrontends.Url
        };

        await _manyFrontendsBffClient.GetAsync("/bff/login")
            .EnsureStatusCode();

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
    public async Task DirectToApi() => await _directHttpClient.GetAsync("/")
            .EnsureStatusCode();

    [Benchmark]
    public async Task YarpProxy() => await _yarpHttpClient.GetAsync("/yarp/test")
            .EnsureStatusCode();

    [Benchmark]
    public async Task BffUserToken() => await _authenticatedBffClient
            .GetWithCSRF("/user_token")
            .EnsureStatusCode();

    [Benchmark]
    public async Task BffWithManyFrontends() => await _manyFrontendsBffClient
            .GetWithCSRF("/user_token")
            .EnsureStatusCode();


    [Benchmark]
    public async Task BffClientCredentialsToken() => await _authenticatedBffClient
            .GetWithCSRF("/client_token")
            .EnsureStatusCode();


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

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> GetWithCSRF(this HttpClient client, string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Headers =
            {
                {"x-csrf", "1"}
            }
        };
        return client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> EnsureStatusCode(this Task<HttpResponseMessage> task, HttpStatusCode? statusCode = HttpStatusCode.OK)
    {
        var response = await task;
        if (response.StatusCode != statusCode)
        {
            throw new HttpRequestException($"Expected status code {statusCode}, but got {response.StatusCode}");
        }
        return response;
    }
}

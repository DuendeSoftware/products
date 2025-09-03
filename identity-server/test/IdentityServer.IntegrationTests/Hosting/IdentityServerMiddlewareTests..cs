// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;


namespace IntegrationTests.Hosting;

public class FailRouter : IEndpointRouter
{
    private readonly Type _exceptionType;
    public FailRouter(Type exceptionType) => _exceptionType = exceptionType;

    public IEndpointHandler Find(HttpContext context) => throw (Exception)_exceptionType.GetConstructor([]).Invoke(null);
}

public class IdentityServerMiddlewareTests
{
    private IdentityServerPipeline _pipeline = new IdentityServerPipeline();
    public static readonly TheoryData<Type, bool> ExceptionFilteringTestCases = new TheoryData<Type, bool>
    {
        { typeof(TaskCanceledException), true },
        { typeof(OperationCanceledException), true },
        { typeof(Exception), false },
        { typeof(InvalidOperationException), false },
        { typeof(ArgumentException), false },
        { typeof(NullReferenceException), false }
    };

    [Theory]
    [MemberData(nameof(ExceptionFilteringTestCases))]
    public async Task expected_exception_types_are_filtered_from_logs_when_incoming_requests_are_canceled(Type t, bool filteringExpected)
    {
        // Set up the pipeline so that we will throw some exception. Throwing in
        // the router specifically is not important - that is just a convenient
        // place to throw an exception.
        _pipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<IEndpointRouter>(_ => new FailRouter(t));
        };
        _pipeline.Initialize(enableLogging: true);

        // First we make a request that is canceled. Filtered exception types are only filtered for canceled Http requests.
        var source = new CancellationTokenSource();
        var token = source.Token;
        source.Cancel();
        var canceledHttpRequest = async () => await _pipeline.BackChannelClient.GetAsync(IdentityServerPipeline.DiscoveryEndpoint, token);

        // The middleware will always throw
        var exceptionShould = await canceledHttpRequest.ShouldThrowAsync<Exception>();

        // The middleware will log most exceptions, but not TaskCanceled or OperationCanceled
        if (filteringExpected)
        {
            _pipeline.MockLogger.LogMessages.ShouldNotContain(m => m.StartsWith("Unhandled exception: "));
        }
        else
        {
            _pipeline.MockLogger.LogMessages.ShouldContain(m => m.StartsWith("Unhandled exception: "));
        }

        // Now reset the log messages so that we can verify that we always log for requests that are not canceled
        _pipeline.MockLogger.LogMessages.Clear();
        var notCanceledRequest = async () => await _pipeline.BackChannelClient.GetAsync(IdentityServerPipeline.DiscoveryKeysEndpoint, CancellationToken.None);
        await notCanceledRequest.ShouldThrowAsync<Exception>();
        _pipeline.MockLogger.LogMessages.ShouldContain(m => m.StartsWith("Unhandled exception: "));
    }


    public static readonly TheoryData<string, bool> ActivityDisplayNameTestCases = new TheoryData<string, bool>
    {
        { "/.well-known/openid-configuration", true },
        { "/connect/token", true },
        { "/connect/garbage", false },
        { Constants.UIConstants.DefaultRoutePaths.Login.EnsureLeadingSlash(), false}
    };

    [Theory]
    [MemberData(nameof(ActivityDisplayNameTestCases))]
    public async Task routed_requests_should_set_http_activity_display_name(string path, bool displayNameShouldBeSet)
    {
        // Build a separate pipeline for this test, because we're going to capture
        // the activity in middleware. A distinct pipeline ensures that we don't share
        // the middleware that captures the activity with other tests running in parallel.
        var pipeline = new IdentityServerPipeline();
        var testActivityMiddleware = new MockTestActivityMiddleware();
        pipeline.OnPreConfigure += builder => builder.Use(next => context => testActivityMiddleware.Handle(context, next));
        pipeline.Initialize();

        await pipeline.BackChannelClient.GetAsync($"https://server{path}");

        if (displayNameShouldBeSet)
        {
            testActivityMiddleware.CapturedActivity.DisplayName.ShouldBe($"GET {path}");
        }
    }

    [Fact]
    public async Task routed_mtls_requests_should_set_http_activity_display_name()
    {
        // Build a separate pipeline for this test, because we're going to capture
        // the activity in middleware. A distinct pipeline ensures that we don't share
        // the middleware that captures the activity with other tests running in parallel.
        var pipeline = new IdentityServerPipeline();
        var testActivityMiddleware = new MockTestActivityMiddleware();
        pipeline.OnPreConfigure += builder => builder.Use(next => context => testActivityMiddleware.Handle(context, next));
        pipeline.Initialize();

        var clientCert = TestCert.Load();
        pipeline.SetClientCertificate(clientCert);

        await pipeline.MtlsBackChannelClient.GetAsync(IdentityServerPipeline.TokenMtlsEndpoint);

        var path = IdentityServerPipeline.TokenMtlsEndpoint.Substring(14); // trim off "https://server"
        testActivityMiddleware.CapturedActivity.DisplayName.ShouldBe($"GET {path}");
    }
}

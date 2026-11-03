// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Yarp.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Duende.Bff.Tests.Endpoints;

/// <summary>
/// Direct, non-HTTP unit tests for the YARP <c>AntiForgeryMiddleware</c>, focused on when it
/// does and doesn't record <see cref="Constants.Middleware.YarpAntiForgeryCheckedEndpoint"/>. See
/// https://github.com/DuendeSoftware/products-private/issues/3885.
/// </summary>
public class YarpAntiForgeryMiddlewareTests
{
    private static (Endpoint endpoint, HttpContext context) CreateContext(bool withAntiforgeryCheck)
    {
        var routeConfig = new RouteConfig
        {
            RouteId = "test-route",
            ClusterId = "test-cluster",
            Match = new RouteMatch { Path = "/test" },
            Metadata = withAntiforgeryCheck
                ? new Dictionary<string, string> { [Constants.Yarp.AntiforgeryCheckMetadata] = "true" }
                : null
        };
        var routeModel = new RouteModel(routeConfig, cluster: null, HttpTransformer.Empty);

        // AntiForgeryMiddleware reads the route via the YARP IReverseProxyFeature (set by YARP's
        // own routing, not via endpoint metadata), and separately records the *endpoint* that was
        // selected (HttpContext.GetEndpoint()) - so both need to be set up independently here.
        var endpoint = new Endpoint(null, EndpointMetadataCollection.Empty, "test-route-endpoint");
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature { Route = routeModel });

        return (endpoint, context);
    }

    private static (AntiForgeryMiddleware middleware, Func<bool> nextCalled) CreateMiddleware()
    {
        var called = false;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        var middleware = new AntiForgeryMiddleware(next, Options.Create(new BffOptions()), NullLogger<AntiForgeryMiddleware>.Instance);
        return (middleware, () => called);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task route_without_antiforgery_metadata_never_sets_the_checked_endpoint_marker(bool headerPresent)
    {
        var (_, context) = CreateContext(withAntiforgeryCheck: false);
        if (headerPresent)
        {
            context.Request.Headers["X-CSRF"] = "1";
        }

        var (middleware, nextCalled) = CreateMiddleware();

        await middleware.Invoke(context);

        nextCalled().ShouldBeTrue();
        context.Items.ShouldNotContainKey(Constants.Middleware.YarpAntiForgeryCheckedEndpoint);
    }

    [Fact]
    public async Task route_with_antiforgery_metadata_and_valid_header_sets_the_checked_endpoint_marker_to_the_endpoint()
    {
        var (endpoint, context) = CreateContext(withAntiforgeryCheck: true);
        context.Request.Headers["X-CSRF"] = "1";

        var (middleware, nextCalled) = CreateMiddleware();

        await middleware.Invoke(context);

        nextCalled().ShouldBeTrue();
        context.Items[Constants.Middleware.YarpAntiForgeryCheckedEndpoint].ShouldBeSameAs(endpoint);
    }

    [Fact]
    public async Task route_with_antiforgery_metadata_and_missing_header_rejects_and_never_sets_the_checked_endpoint_marker()
    {
        var (_, context) = CreateContext(withAntiforgeryCheck: true);
        // No X-CSRF header.

        var (middleware, nextCalled) = CreateMiddleware();

        await middleware.Invoke(context);

        nextCalled().ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Items.ShouldNotContainKey(Constants.Middleware.YarpAntiForgeryCheckedEndpoint);
    }
}

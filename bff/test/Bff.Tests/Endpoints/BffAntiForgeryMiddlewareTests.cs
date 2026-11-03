// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Endpoints.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Tests.Endpoints;

/// <summary>
/// Direct, non-HTTP unit tests for <c>BffAntiForgeryMiddleware</c>, focused on when it does and
/// doesn't record <see cref="Constants.Middleware.AntiForgeryMarker"/>, and how
/// <c>CheckForBffMiddleware</c> uses it.
/// </summary>
public class BffAntiForgeryMiddlewareTests
{
    private static Endpoint CreateBffEndpoint() =>
        new(null, new EndpointMetadataCollection(new BffApiAttribute()), "test-bff-endpoint");

    private static Endpoint CreatePlainEndpoint() =>
        new(null, new EndpointMetadataCollection(), "test-plain-endpoint");

    private static async Task<bool> InvokeMiddleware(HttpContext context)
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new BffAntiForgeryMiddleware(next, Options.Create(new BffOptions()), NullLogger<BffAntiForgeryMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        return nextCalled;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task no_endpoint_selected_does_not_set_the_marker(bool headerPresent)
    {
        var context = new DefaultHttpContext();
        // No endpoint set - simulates running before UseRouting() selected one.
        if (headerPresent)
        {
            context.Request.Headers["X-CSRF"] = "1";
        }

        var nextCalled = await InvokeMiddleware(context);

        nextCalled.ShouldBeTrue();
        context.Items.ShouldNotContainKey(Constants.Middleware.AntiForgeryMarker);
        _ = Should.Throw<InvalidOperationException>(() => context.CheckForBffMiddleware(new BffOptions()));
    }

    [Fact]
    public async Task bff_endpoint_with_valid_header_sets_the_marker_to_the_endpoint()
    {
        var endpoint = CreateBffEndpoint();
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        context.Request.Headers["X-CSRF"] = "1";

        var nextCalled = await InvokeMiddleware(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Items[Constants.Middleware.AntiForgeryMarker].ShouldBeSameAs(endpoint);
        context.BffMiddlewareRanForEndpoint().ShouldBeTrue();
    }

    [Fact]
    public async Task non_bff_endpoint_sets_the_marker_so_management_endpoints_pass_the_middleware_check()
    {
        var endpoint = CreatePlainEndpoint();
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);

        var nextCalled = await InvokeMiddleware(context);

        nextCalled.ShouldBeTrue();
        context.Items[Constants.Middleware.AntiForgeryMarker].ShouldBeSameAs(endpoint);
        Should.NotThrow(() => context.CheckForBffMiddleware(new BffOptions()));
    }

    [Fact]
    public async Task bff_endpoint_without_header_is_rejected()
    {
        var endpoint = CreateBffEndpoint();
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        // No X-CSRF header.

        var nextCalled = await InvokeMiddleware(context);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void marker_for_a_different_endpoint_fails_the_middleware_check()
    {
        var context = new DefaultHttpContext();
        context.Items[Constants.Middleware.AntiForgeryMarker] = CreateBffEndpoint();
        context.SetEndpoint(CreateBffEndpoint());

        context.BffMiddlewareRanForEndpoint().ShouldBeFalse();
        _ = Should.Throw<InvalidOperationException>(() => context.CheckForBffMiddleware(new BffOptions()));
    }
}

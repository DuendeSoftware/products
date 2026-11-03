// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Yarp.Internal;

namespace Duende.Bff.Tests.Endpoints;

/// <summary>
/// Focused, non-HTTP unit tests for <c>AccessTokenTransformProvider.IsRouteProtected</c>, the
/// internal detection logic behind the YARP anti-forgery warning. These directly exercise the
/// marker semantics described in https://github.com/DuendeSoftware/products-private/issues/3885,
/// including the marker left by UseBff() for a different endpoint.
/// </summary>
public class AccessTokenTransformProviderIsRouteProtectedTests
{
    private static Endpoint CreateEndpoint(params object[] metadata) =>
        new(null, new EndpointMetadataCollection(metadata), "test-endpoint");

    [Fact]
    public void no_endpoint_and_no_antiforgery_metadata_is_not_protected()
    {
        var httpContext = new DefaultHttpContext();

        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: false, out var configuredButNotEnabled);

        protectedResult.ShouldBeFalse();
        configuredButNotEnabled.ShouldBeFalse();
    }

    [Fact]
    public void endpoint_with_skip_antiforgery_metadata_is_always_protected()
    {
        var endpoint = CreateEndpoint(new BffApiAttribute(), new BffApiSkipAntiforgeryAttribute());
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);

        // Even with no checked-endpoint marker at all, this is explicit opt-out.
        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: false, out var configuredButNotEnabled);

        protectedResult.ShouldBeTrue();
        configuredButNotEnabled.ShouldBeFalse();
    }

    [Fact]
    public void bff_endpoint_that_the_bff_middleware_ran_for_is_protected()
    {
        // UseBff() returns 401 for a BFF API endpoint without the header, so if it ran for this
        // endpoint and the request got this far, the header check passed.
        var endpoint = CreateEndpoint(new BffApiAttribute());
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);
        httpContext.Items[Constants.Middleware.AntiForgeryMarker] = endpoint;

        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: false, out var configuredButNotEnabled);

        protectedResult.ShouldBeTrue();
        configuredButNotEnabled.ShouldBeFalse();
    }

    [Fact]
    public void non_bff_endpoint_that_the_bff_middleware_ran_for_is_not_protected()
    {
        // UseBff() runs for every endpoint, but only checks the header on BFF API endpoints.
        var endpoint = CreateEndpoint();
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);
        httpContext.Items[Constants.Middleware.AntiForgeryMarker] = endpoint;

        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: false, out var configuredButNotEnabled);

        protectedResult.ShouldBeFalse();
        configuredButNotEnabled.ShouldBeFalse();
    }

    [Fact]
    public void bff_middleware_marker_for_a_different_endpoint_instance_is_not_protected()
    {
        var markedEndpoint = CreateEndpoint(new BffApiAttribute());
        var currentEndpoint = CreateEndpoint(new BffApiAttribute());
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(currentEndpoint);
        httpContext.Items[Constants.Middleware.AntiForgeryMarker] = markedEndpoint;

        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: false, out var configuredButNotEnabled);

        protectedResult.ShouldBeFalse();
        configuredButNotEnabled.ShouldBeFalse();
    }

    [Fact]
    public void yarp_checked_marker_for_a_different_endpoint_instance_is_not_protected()
    {
        var checkedEndpoint = CreateEndpoint();
        var currentEndpoint = CreateEndpoint();
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(currentEndpoint);
        httpContext.Items[Constants.Middleware.YarpAntiForgeryCheckedEndpoint] = checkedEndpoint;

        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: true, out var configuredButNotEnabled);

        protectedResult.ShouldBeFalse();
        configuredButNotEnabled.ShouldBeTrue();
    }

    [Fact]
    public void antiforgery_check_metadata_without_a_recorded_check_is_configured_but_not_enabled()
    {
        var httpContext = new DefaultHttpContext();

        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: true, out var configuredButNotEnabled);

        protectedResult.ShouldBeFalse();
        configuredButNotEnabled.ShouldBeTrue();
    }

    [Fact]
    public void antiforgery_check_metadata_with_a_recorded_check_is_protected()
    {
        var endpoint = CreateEndpoint();
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);
        httpContext.Items[Constants.Middleware.YarpAntiForgeryCheckedEndpoint] = endpoint;

        var protectedResult = AccessTokenTransformProvider.IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata: true, out var configuredButNotEnabled);

        protectedResult.ShouldBeTrue();
        configuredButNotEnabled.ShouldBeFalse();
    }
}

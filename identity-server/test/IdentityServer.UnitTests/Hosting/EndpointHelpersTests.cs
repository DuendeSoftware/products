// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using UnitTests.Common;

namespace UnitTests.Hosting;

public class EndpointHelpersTests
{
    [Fact]
    public void OnRouteMatched_WhenRouteDoesNotStartWithWellKnown_ReturnsFalse()
    {
        var routeValues = new RouteValueDictionary { { "subPath", "metadata" } };
        var serverUrlsMock = new MockServerUrls();
        var services = new ServiceCollection();
        services.AddSingleton<IServerUrls>(serverUrlsMock);
        var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            Request =
            {
                PathBase = new PathString("/identity")
            },
            RequestServices = serviceProvider
        };

        var result = EndpointHelpers.OAuth2AuthorizationServerMetadataHelpers.OnRouteMatched(context, routeValues, new FakeLogger());

        result.ShouldBeFalse();
    }

    [Fact]
    public void OnRouteMatched_WhenSubPathIsValidString_SetsBasePath()
    {
        var routeValues = new RouteValueDictionary { { "subPath", "metadata" } };
        var serverUrlsMock = new MockServerUrls();
        var services = new ServiceCollection();
        services.AddSingleton<IServerUrls>(serverUrlsMock);
        var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };

        var result = EndpointHelpers.OAuth2AuthorizationServerMetadataHelpers.OnRouteMatched(context, routeValues, new FakeLogger());

        result.ShouldBeTrue();
        serverUrlsMock.BasePath.ShouldBe("/metadata");
    }

    [Fact]
    public void OnRouteMatched_WhenSubPathIsMissing_DoesNothing()
    {
        var routeValues = new RouteValueDictionary();
        var serverUrlsMock = new MockServerUrls();
        var services = new ServiceCollection();
        services.AddSingleton<IServerUrls>(serverUrlsMock);
        var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };

        var result = EndpointHelpers.OAuth2AuthorizationServerMetadataHelpers.OnRouteMatched(context, routeValues, new FakeLogger());

        result.ShouldBeTrue();
        serverUrlsMock.BasePath.ShouldBeNull();
    }

    [Fact]
    public void OnRouteMatched_WhenSubPathIsNotString_SetsBasePath()
    {
        var routeValues = new RouteValueDictionary { { "subPath", 123 } };
        var serverUrlsMock = new MockServerUrls();
        var services = new ServiceCollection();
        services.AddSingleton<IServerUrls>(serverUrlsMock);
        var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };

        var result = EndpointHelpers.OAuth2AuthorizationServerMetadataHelpers.OnRouteMatched(context, routeValues, new FakeLogger());

        result.ShouldBeTrue();
        serverUrlsMock.BasePath.ShouldBe("/123");
    }
}

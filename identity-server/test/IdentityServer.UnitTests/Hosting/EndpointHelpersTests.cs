// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Hosting;

public class EndpointHelpersTests
{
    [InlineData("/.WELL-KNOWN/OAUTH-AUTHORIZATION-SERVER")]
    [InlineData("/.well-known/oauth-authorization-server")]
    [Theory]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathStartsWithWellKnown_ReturnsTrue(string path)
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = path
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeTrue();
    }

    [Fact]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathStartsWithWellKnownWithTrailingSlash_ReturnsTrue()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/.well-known/oauth-authorization-server/"
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeTrue();
    }

    [Fact]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathStartsWithWellKnownAndHasMoreSegments_ReturnsTrue()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/.well-known/oauth-authorization-server/extra"
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeTrue();
    }

    [Fact]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathStartsWithSegmentWithExtraInIt_ReturnsFalse()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/.well-known/extra/oauth-authorization-server-extra"
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeFalse();
    }

    [Fact]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathIsEmpty_ReturnsFalse()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = ""
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeFalse();
    }

    [Fact]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathIsRoot_ReturnsFalse()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/"
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeFalse();
    }

    [Fact]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathIsNull_ReturnsFalse()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = null
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeFalse();
    }

    [Fact]
    public void OAuthMetadataEndpoint_IsMatch_WhenPathDoesNotStartWithWellKnown_ReturnsFalse()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/identity/metadata"
            }
        };

        var result = EndpointHelpers.OAuthMetadataHelpers.IsMatch(context);

        result.ShouldBeFalse();
    }
}

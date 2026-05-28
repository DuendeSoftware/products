// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
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

public sealed class SamlMetadataHelpersTests
{
    [Fact]
    public void ResolveMetadataPath_ReturnsEntityIdPath_WhenEntityIdIsNull()
    {
        var options = new SamlOptions { EntityId = null, EntityIdPath = "/my-idp" };

        var path = EndpointHelpers.SamlMetadataHelpers.ResolveMetadataPath(options);

        path.ShouldBe("/my-idp");
    }

    [Fact]
    public void ResolveMetadataPath_ReturnsPathComponent_WhenEntityIdIsHttpsUrl()
    {
        var options = new SamlOptions { EntityId = "https://idp.example.com/custom/saml" };

        var path = EndpointHelpers.SamlMetadataHelpers.ResolveMetadataPath(options);

        path.ShouldBe("/custom/saml");
    }

    [Fact]
    public void ResolveMetadataPath_ReturnsPathComponent_WhenEntityIdIsHttpUrl()
    {
        var options = new SamlOptions { EntityId = "http://idp.example.com/saml2" };

        var path = EndpointHelpers.SamlMetadataHelpers.ResolveMetadataPath(options);

        path.ShouldBe("/saml2");
    }

    [Fact]
    public void ResolveMetadataPath_FallsBackToEntityIdPath_WhenEntityIdIsUrn()
    {
        var options = new SamlOptions { EntityId = "urn:my:custom:idp", EntityIdPath = "/fallback" };

        var path = EndpointHelpers.SamlMetadataHelpers.ResolveMetadataPath(options);

        path.ShouldBe("/fallback");
    }

    [Fact]
    public void ResolveMetadataPath_FallsBackToEntityIdPath_WhenEntityIdIsUrlWithNoPath()
    {
        var options = new SamlOptions { EntityId = "https://idp.example.com", EntityIdPath = "/my-idp" };

        var path = EndpointHelpers.SamlMetadataHelpers.ResolveMetadataPath(options);

        path.ShouldBe("/my-idp");
    }

    [Fact]
    public void ResolveMetadataPath_UsesDefaultEntityIdPath_WhenEntityIdIsNull()
    {
        var options = new SamlOptions();

        var path = EndpointHelpers.SamlMetadataHelpers.ResolveMetadataPath(options);

        path.ShouldBe("/Saml2");
    }
}

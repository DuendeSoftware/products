// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Http;
using UnitTests.Common;
using System.Threading.Tasks;
using Xunit;

public class MutualTlsEndpointMiddlewareTests
{
    private readonly IdentityServerOptions _options;
    private readonly MutualTlsEndpointMiddleware _middleware;
    private readonly HttpContext _httpContext;

    public MutualTlsEndpointMiddlewareTests()
    {
        var testLogger = TestLogger.Create<MutualTlsEndpointMiddleware>();
        _options = TestIdentityServerOptions.Create();
        _middleware = new MutualTlsEndpointMiddleware(
            next: (ctx) => Task.CompletedTask,
            options: _options,
            logger: testLogger
        );
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public void mtls_endpoint_type_when_mtls_disabled_should_be_none()
    {
        _options.MutualTls.Enabled = false;
        var result = _middleware.DetermineMtlsEndpointType(_httpContext, out var subPath);
        Assert.Equal(MutualTlsEndpointMiddleware.MtlsEndpointType.None, result);
        Assert.Null(subPath);
    }

    [Theory]
    [InlineData("mtls.example.com")]
    [InlineData("mtls.example.com:443")]
    [InlineData("mtls.example.com:5001")]
    public void mtls_endpoint_type_separate_domain_should_be_detected(string host)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        _httpContext.Request.Host = new HostString(host);

        // Act
        var result = _middleware.DetermineMtlsEndpointType(_httpContext, out var subPath);

        // Assert
        Assert.Equal(MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain, result);
        Assert.Null(subPath);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("example.com:443")]
    [InlineData("example.com:5001")]
    [InlineData("other.example.com")]
    [InlineData("other.example.com:443")]
    public void mtls_endpoint_type_separate_domain_should_not_match_different_domain(string host)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        _httpContext.Request.Host = new HostString(host);

        // Act
        var result = _middleware.DetermineMtlsEndpointType(_httpContext, out var subPath);

        // Assert
        Assert.Equal(MutualTlsEndpointMiddleware.MtlsEndpointType.None, result);
        Assert.Null(subPath);
    }

    [Theory]
    [InlineData("mtls.example.com")]
    [InlineData("mtls.example.com:443")]
    [InlineData("mtls.example.com:5001")]
    public void mtls_endpoint_type_subdomain_should_be_detected(string host)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls";
        _httpContext.Request.Host = new HostString(host);

        // Act
        var result = _middleware.DetermineMtlsEndpointType(_httpContext, out var subPath);

        // Assert
        Assert.Equal(MutualTlsEndpointMiddleware.MtlsEndpointType.Subdomain, result);
        Assert.Null(subPath);
    }

    [Theory]
    [InlineData("api.example.com")]
    [InlineData("api.example.com:443")]
    [InlineData("example.com")]
    [InlineData("example.com:5001")]
    public void mtls_endpoint_type_subdomain_should_not_match_different_subdomain(string host)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls";
        _httpContext.Request.Host = new HostString(host);

        // Act
        var result = _middleware.DetermineMtlsEndpointType(_httpContext, out var subPath);

        // Assert
        Assert.Equal(MutualTlsEndpointMiddleware.MtlsEndpointType.None, result);
        Assert.Null(subPath);
    }

    [Fact]
    public void mtls_endpoint_type_path_based_should_be_detected()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _httpContext.Request.Path = new PathString("/connect/mtls/token");

        // Act
        var result = _middleware.DetermineMtlsEndpointType(_httpContext, out var subPath);

        // Assert
        Assert.Equal(MutualTlsEndpointMiddleware.MtlsEndpointType.PathBased, result);
        Assert.Equal("/token", subPath!.Value);
    }

    [Fact]
    public void mtls_endpoint_type_should_be_none_when_enabled_but_no_matching_configuration()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        _httpContext.Request.Host = new HostString("regular.example.com");
        _httpContext.Request.Path = new PathString("/connect/token");

        // Act
        var result = _middleware.DetermineMtlsEndpointType(_httpContext, out var subPath);

        // Assert
        Assert.Equal(MutualTlsEndpointMiddleware.MtlsEndpointType.None, result);
        Assert.Null(subPath);
    }
}
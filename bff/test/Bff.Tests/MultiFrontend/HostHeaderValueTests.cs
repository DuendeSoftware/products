// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;

namespace Duende.Bff.Tests.MultiFrontend;

public class HostHeaderValueTests
{
    [Fact]
    public void Parse_WithValidHttpsUrl_SetsCorrectProperties()
    {
        // Arrange
        var url = "https://example.com";

        // Act
        var origin = HostHeaderValue.Parse(url);

        // Assert
        origin.Scheme.ShouldBe("https");
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(443); // Default HTTPS port
    }

    [Theory]
    [InlineData("https://example.com:888", "example.com", 888)]
    [InlineData("https://example.com:443", "example.com", 443)]
    [InlineData("http://example.com", "example.com", 80)]
    [InlineData("http://example.com:80", "example.com", 80)]
    [InlineData("http://example.com:888", "example.com", 888)]
    public void ToHostStringHandlesDefaultPorts(string url, string hoststring, int port)
    {
        var host = HostHeaderValue.Parse(url).ToHostString();

        host.ShouldBe(new HostString(hoststring, port));
    }

    [Fact]
    public void Parse_WithValidHttpUrl_SetsCorrectProperties()
    {
        // Arrange
        var url = "http://example.com";

        // Act
        var origin = HostHeaderValue.Parse(url);

        // Assert
        origin.Scheme.ShouldBe("http");
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(80); // Default HTTP port in Uri
    }

    [Fact]
    public void Equals_can_handle_default_ports()
    {
        var request = CreateHttpRequest("https", "some_host");
        var origin = HostHeaderValue.Parse("https://some_host");

        origin.Equals(request).ShouldBeTrue();
    }

    [Fact]
    public void Equals_can_handle_explicit_ports()
    {
        var request = CreateHttpRequest("https", "some_host", 443);
        var origin = HostHeaderValue.Parse("https://some_host");

        origin.Equals(request).ShouldBeTrue();
    }

    [Fact]
    public void Parse_WithCustomPort_SetsCorrectPort()
    {
        // Arrange
        var url = "https://example.com:8443";

        // Act
        var origin = HostHeaderValue.Parse(url);

        // Assert
        origin.Port.ShouldBe(8443);
    }

    [Fact]
    public void Parse_WithPath_IgnoresPath()
    {
        // Arrange
        var url = "https://example.com/some/path";

        // Act
        var origin = HostHeaderValue.Parse(url);

        // Assert
        origin.Scheme.ShouldBe("https");
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(443);
    }

    [Fact]
    public void Parse_WithQuery_IgnoresQuery()
    {
        // Arrange
        var url = "https://example.com?param=value";

        // Act
        var origin = HostHeaderValue.Parse(url);

        // Assert
        origin.Scheme.ShouldBe("https");
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(443);
    }

    [Fact]
    public void Parse_WithFragment_IgnoresFragment()
    {
        // Arrange
        var url = "https://example.com#fragment";

        // Act
        var origin = HostHeaderValue.Parse(url);

        // Assert
        origin.Scheme.ShouldBe("https");
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(443);
    }

    [Fact]
    public void Parse_WithInvalidUrl_ThrowsUriFormatException()
    {
        // Arrange
        var invalidUrl = "not-a-url";

        // Act & Assert
        Should.Throw<UriFormatException>(() => HostHeaderValue.Parse(invalidUrl));
    }

    [Fact]
    public void Equals_WithMatchingHttpRequest_ReturnsTrue()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("https://example.com");
        var request = CreateHttpRequest("https://example.com");

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentScheme_ReturnsFalse()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("https://example.com");
        var request = CreateHttpRequest("http://example.com");

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithDifferentHost_ReturnsFalse()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("https://example.com");
        var request = CreateHttpRequest("https://different.com");

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithDifferentPort_ReturnsFalse()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("https://example.com:8443");
        var request = CreateHttpRequest("https://example.com");

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNullRequest_ReturnsFalse()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("https://example.com");

        // Act
        var result = origin.Equals((HostHeaderValue?)null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Equals_HostComparisonIsCaseInsensitive()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("https://EXAMPLE.com");
        var request = CreateHttpRequest("https://example.COM");

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_SchemeComparisonIsCaseInsensitive()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("HTTPS://example.com");
        var request = CreateHttpRequest("https://example.com");

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_IgnoresPathInRequest()
    {
        // Arrange
        var origin = HostHeaderValue.Parse("https://example.com");
        var request = CreateHttpRequest("https://example.com/some/path");

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Origin_WithExplicitInitialization_SetsProperties()
    {
        // Arrange & Act
        var origin = new HostHeaderValue
        {
            Scheme = "https",
            Host = "example.com",
            Port = 8443
        };

        // Assert
        origin.Scheme.ShouldBe("https");
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(8443);
    }

    // Helper method to create HttpRequest with specified URL
    private static HttpRequest CreateHttpRequest(string url)
    {
        var uri = new Uri(url);
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Scheme = uri.Scheme;
        httpContext.Request.Host = new HostString(uri.Host, uri.Port);
        if (uri.AbsolutePath != "/")
        {
            httpContext.Request.Path = uri.AbsolutePath;
        }

        return httpContext.Request;
    }

    private static HttpRequest CreateHttpRequest(string scheme, string host, int? port = null, string path = "/")
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Scheme = scheme;
        httpContext.Request.Host = port == null ? new HostString(host) : new HostString(host, port.Value);
        httpContext.Request.Path = path;
        return httpContext.Request;
    }
}

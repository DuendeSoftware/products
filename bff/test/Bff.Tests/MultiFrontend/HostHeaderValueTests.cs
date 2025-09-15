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
    public void Parse_WithFragment_IgnoresFragment()
    {
        // Arrange
        var url = "https://example.com#fragment";

        // Act
        var origin = HostHeaderValue.Parse(url);

        // Assert
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(443);
    }


    [Theory]
    [InlineData("https://uri", "https://uri", true, "same uri")]
    [InlineData("uri:443", "https://uri", true, "default https port")]
    [InlineData("http://uri", "http://uri", true, "default https port")]
    [InlineData("http://uri", "https://uri", false, "different scheme")]
    [InlineData("https://uri:123", "https://uri:321", false, "different port")]
    [InlineData("https://uri:123", "https://different:123", false, "different host, same port")]
    [InlineData("https://uri", "https://different", false, "different host, no port")]
    [InlineData("wss://uri", "wss://uri", true, "Uri with websockets")]
    public void Can_compare_with_http_request(string input, string requestUri, bool matches, string reason)
    {
        // Arrange
        var origin = HostHeaderValue.Parse(input);
        var request = CreateHttpRequest(requestUri);

        // Act
        var result = origin.Equals(request);

        // Assert
        result.ShouldBe(matches, reason);
    }

    [Theory]
    [InlineData("not:a", "not a valid host header")]
    [InlineData("http:/not", "not a valid host header")]
    public void Will_catch_invalid_hostheader(string input, string reason)
    {
        var action= () => HostHeaderValue.Parse(input);
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Origin_WithExplicitInitialization_SetsProperties()
    {
        // Arrange & Act
        var origin = new HostHeaderValue
        {
            Host = "example.com",
            Port = 8443
        };

        // Assert
        origin.Host.ShouldBe("example.com");
        origin.Port.ShouldBe(8443);
    }

    [Theory]
    [InlineData("host.com", "host.com", 443)]
    [InlineData("host.com:443", "host.com", 443)]
    [InlineData("host.com:5000", "host.com", 5000)]
    [InlineData("host.com:80", "host.com", 80)]
    [InlineData("https://host.com:80", "host.com", 80)]
    [InlineData("http://host.com:80", "host.com", 80)]
    [InlineData("https://host.com", "host.com", 443)]
    [InlineData("https://host.com:443", "host.com", 443)]
    public void Can_parse_host_header(string input, string host, int port)
    {
        var parsed = HostHeaderValue.Parse(input);

        parsed.Host.ShouldBe(host);
        parsed.Port.ShouldBe(port);
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

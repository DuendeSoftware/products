// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Hosting;

public class MutualTlsEndpointMiddlewareTests
{
    private const string Category = "Hosting - MutualTls Endpoint Middleware";

    private readonly IdentityServerOptions _options;
    private readonly ILogger<MutualTlsEndpointMiddleware> _logger;
    private bool _nextWasCalled;
    private MutualTlsEndpointMiddleware _subject;

    public MutualTlsEndpointMiddlewareTests()
    {
        _options = new IdentityServerOptions();
        _logger = TestLogger.Create<MutualTlsEndpointMiddleware>();
        _nextWasCalled = false;

        _subject = new MutualTlsEndpointMiddleware(Next, _options, _logger);
    }

    private Task Next(HttpContext context)
    {
        _nextWasCalled = true;
        return Task.CompletedTask;
    }

    private DefaultHttpContext CreateContext(string host = "localhost", string path = "/")
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = new PathString(path);
        context.Request.Scheme = "https";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private DefaultHttpContext CreateContextWithSuccessfulAuthentication(string host = "localhost", string path = "/", string scheme = "Certificate")
    {
        var context = CreateContext(host, path);

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test")], "Certificate"));
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, scheme));

        var mockAuthService = new MockAuthenticationService { Result = authResult };
        var mockServiceProvider = new MockServiceProvider(mockAuthService);

        context.RequestServices = mockServiceProvider;
        return context;
    }

    private DefaultHttpContext CreateContextWithFailedAuthentication(string host = "localhost", string path = "/", string scheme = "Certificate", string errorMessage = "Certificate validation failed")
    {
        var context = CreateContext(host, path);

        var authResult = AuthenticateResult.Fail(errorMessage);
        var mockAuthService = new MockAuthenticationService { Result = authResult };
        var mockServiceProvider = new MockServiceProvider(mockAuthService);

        context.RequestServices = mockServiceProvider;
        return context;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_mtls_disabled_should_call_next_without_authentication()
    {
        // Arrange
        _options.MutualTls.Enabled = false;
        // Setup auth failure in the context to check that auth doesn't occur
        var context = CreateContextWithFailedAuthentication();

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_mtls_enabled_no_domain_or_path_match_should_call_next_without_authentication()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        // Setup auth failure in the context to check that auth doesn't occur
        var context = CreateContextWithFailedAuthentication("localhost", "/api/test");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_separate_domain_exact_match_successful_auth_should_call_next()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithSuccessfulAuthentication("mtls.example.com", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_separate_domain_exact_match_failed_auth_should_return_400()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithFailedAuthentication("mtls.example.com", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(400);

        var responseBody = await GetResponseBodyAsString(context);
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
        errorResponse.GetProperty("error").GetString().ShouldBe("invalid_client");
        errorResponse.GetProperty("error_description").GetString().ShouldBe("mTLS authentication failed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_separate_domain_case_insensitive_match_should_authenticate()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithSuccessfulAuthentication("MTLS.EXAMPLE.COM", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_separate_domain_no_match_should_not_authenticate()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";

        // Setup auth failure in the context to check that auth doesn't occur
        var context = CreateContextWithFailedAuthentication("api.example.com", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_subdomain_match_successful_auth_should_call_next()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls"; // subdomain without dot
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithSuccessfulAuthentication("mtls.example.com", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_subdomain_match_failed_auth_should_return_400()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls"; // subdomain without dot
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithFailedAuthentication("mtls.example.com", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(400);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_subdomain_case_insensitive_match_should_authenticate()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls"; // subdomain without dot
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithSuccessfulAuthentication("MTLS.EXAMPLE.COM", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_subdomain_no_match_should_not_authenticate()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls"; // subdomain without dot

        // Setup auth failure in the context to check that auth doesn't occur
        var context = CreateContextWithFailedAuthentication("api.example.com", "/connect/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_path_based_mtls_successful_auth_should_rewrite_path_and_call_next()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithSuccessfulAuthentication("localhost", "/connect/mtls/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
        context.Request.Path.ToString().ShouldBe("/connect/token");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_path_based_mtls_failed_auth_should_return_400()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithFailedAuthentication("localhost", "/connect/mtls/token");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(400);
        context.Request.Path.ToString().ShouldBe("/connect/mtls/token"); // Path should not be rewritten on failure
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("/connect/mtls/token", "/connect/token")]
    [InlineData("/connect/mtls/revocation", "/connect/revocation")]
    [InlineData("/connect/mtls/introspect", "/connect/introspect")]
    [InlineData("/connect/mtls/deviceauthorization", "/connect/deviceauthorization")]
    public async Task Invoke_path_based_mtls_should_rewrite_paths_correctly(string originalPath, string expectedPath)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithSuccessfulAuthentication("localhost", originalPath);

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
        context.Request.Path.ToString().ShouldBe(expectedPath);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_path_based_mtls_non_matching_path_should_not_authenticate()
    {
        // Arrange
        _options.MutualTls.Enabled = true;

        // Setup auth failure in the context to check that auth doesn't occur
        var context = CreateContextWithFailedAuthentication("localhost", "/connect/token"); // Not MTLS path

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
        context.Request.Path.ToString().ShouldBe("/connect/token"); // Path unchanged
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_custom_authentication_scheme_should_use_correct_scheme()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        _options.MutualTls.ClientCertificateAuthenticationScheme = "CustomCertificate";

        var context = CreateContextWithSuccessfulAuthentication("mtls.example.com", "/connect/token", "CustomCertificate");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invoke_path_rewriting_should_preserve_leading_slash()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.ClientCertificateAuthenticationScheme = "Certificate";

        var context = CreateContextWithSuccessfulAuthentication("localhost", "/connect/mtls/token/extra/path");

        // Act
        await _subject.Invoke(context, null);

        // Assert
        _nextWasCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
        context.Request.Path.ToString().ShouldBe("/connect/token/extra/path");
        context.Request.Path.ToString().ShouldStartWith("/");
    }

    [Fact]
    internal void mtls_endpoint_type_when_mtls_disabled_should_be_none()
    {
        _options.MutualTls.Enabled = false;
        var context = CreateContext();
        var result = _subject.DetermineMtlsEndpointType(context, out var subPath);
        result.ShouldBe(MutualTlsEndpointMiddleware.MtlsEndpointType.None);
        subPath.ShouldBeNull();
    }

    [Theory]
    [InlineData("mtls.example.com", "mtls.example.com", MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain)]
    [InlineData("mTLS.example.com", "mtls.example.com", MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain)]
    [InlineData("mtls.example.com", "mTLS.example.com", MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain)]
    [InlineData("mtls.example.com:443", "mtls.example.com", MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain)]
    [InlineData("mtls.example.com:5001", "mtls.example.com", MutualTlsEndpointMiddleware.MtlsEndpointType.None)]
    [InlineData("mtls.example.com", "mtls.example.com:443", MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain)]
    [InlineData("mtls.example.com:443", "mtls.example.com:443", MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain)]
    [InlineData("mtls.example.com:5001", "mtls.example.com:443", MutualTlsEndpointMiddleware.MtlsEndpointType.None)]
    [InlineData("mtls.example.com", "mtls.example.com:5001", MutualTlsEndpointMiddleware.MtlsEndpointType.None)]
    [InlineData("mtls.example.com:443", "mtls.example.com:5001", MutualTlsEndpointMiddleware.MtlsEndpointType.None)]
    [InlineData("mtls.example.com:5001", "mtls.example.com:5001", MutualTlsEndpointMiddleware.MtlsEndpointType.SeparateDomain)]
    internal void mtls_endpoint_type_separate_domain_should_be_detected(string requestedHost, string configuredDomainName, MutualTlsEndpointMiddleware.MtlsEndpointType expectedType)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = configuredDomainName;
        var context = CreateContext();
        context.Request.Host = new HostString(requestedHost);

        // Act
        var result = _subject.DetermineMtlsEndpointType(context, out var subPath);

        // Assert
        result.ShouldBe(expectedType);
        subPath.ShouldBeNull();
    }

    [Theory]
    [InlineData("example.com", "mtls.example.com")]
    [InlineData("example.com:443", "mtls.example.com")]
    [InlineData("example.com:5001", "mtls.example.com")]
    [InlineData("other.example.com", "mtls.example.com")]
    [InlineData("other.example.com:443", "mtls.example.com")]
    [InlineData("example.com", "mtls.example.com:443")]
    [InlineData("example.com:443", "mtls.example.com:443")]
    [InlineData("other.example.com", "mtls.example.com:5001")]
    [InlineData("other.example.com:5001", "mtls.example.com:5001")]
    internal void mtls_endpoint_type_separate_domain_should_not_match_different_domain(string requestedHost, string configuredDomainName)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = configuredDomainName;
        var context = CreateContext();
        context.Request.Host = new HostString(requestedHost);

        // Act
        var result = _subject.DetermineMtlsEndpointType(context, out var subPath);

        // Assert
        result.ShouldBe(MutualTlsEndpointMiddleware.MtlsEndpointType.None);
        subPath.ShouldBeNull();
    }

    [Theory]
    [InlineData("mtls.example.com", "mtls")]
    [InlineData("mtls.example.com", "mTLS")]
    [InlineData("mTLS.example.com", "mtls")]
    [InlineData("mtls.example.com:443", "mtls")]
    [InlineData("mtls.example.com:5001", "mtls")]
    internal void mtls_endpoint_type_subdomain_should_be_detected(string requestedHost, string configuredDomainName)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = configuredDomainName;
        var context = CreateContext();
        context.Request.Host = new HostString(requestedHost);

        // Act
        var result = _subject.DetermineMtlsEndpointType(context, out var subPath);

        // Assert
        result.ShouldBe(MutualTlsEndpointMiddleware.MtlsEndpointType.Subdomain);
        subPath.ShouldBeNull();
    }

    [Theory]
    [InlineData("api.example.com", "mtls")]
    [InlineData("api.example.com:443", "mtls")]
    [InlineData("example.com", "mtls")]
    [InlineData("example.com:5001", "mtls")]
    internal void mtls_endpoint_type_subdomain_should_not_match_different_subdomain(string requestedHost, string configuredDomainName)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = configuredDomainName;
        var context = CreateContext();
        context.Request.Host = new HostString(requestedHost);

        // Act
        var result = _subject.DetermineMtlsEndpointType(context, out var subPath);

        // Assert
        result.ShouldBe(MutualTlsEndpointMiddleware.MtlsEndpointType.None);
        subPath.ShouldBeNull();
    }

    [Theory]
    [InlineData("/connect/mtls/token")]
    [InlineData("/connect/mTLS/token")]
    internal void mtls_endpoint_type_path_based_should_be_detected(string requestedPath)
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        var context = CreateContext();
        context.Request.Path = new PathString(requestedPath);

        // Act
        var result = _subject.DetermineMtlsEndpointType(context, out var subPath);

        // Assert
        result.ShouldBe(MutualTlsEndpointMiddleware.MtlsEndpointType.PathBased);
        subPath.Value.ToString().ShouldBe("/token");
    }

    [Fact]
    internal void mtls_endpoint_type_should_be_none_when_enabled_but_no_matching_configuration()
    {
        // Arrange
        _options.MutualTls.Enabled = true;
        _options.MutualTls.DomainName = "mtls.example.com";
        var context = CreateContext();
        context.Request.Host = new HostString("regular.example.com");
        context.Request.Path = new PathString("/connect/token");

        // Act
        var result = _subject.DetermineMtlsEndpointType(context, out var subPath);

        // Assert
        result.ShouldBe(MutualTlsEndpointMiddleware.MtlsEndpointType.None);
        subPath.ShouldBeNull();
    }

    private async Task<string> GetResponseBodyAsString(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private class MockServiceProvider : IServiceProvider
    {
        private readonly IAuthenticationService _authService;

        public MockServiceProvider(IAuthenticationService authService) => _authService = authService;

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IAuthenticationService))
            {
                return _authService;
            }
            return null;
        }
    }
}

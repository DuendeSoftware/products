// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Testing;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation;

public class DefaultIssuerPathValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenIssuerPathMatches_ReturnsTrue()
    {
        var issuerNameService = new TestIssuerNameService("https://example.com/foo");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);
        var path = "/foo";

        var result = await subject.ValidateAsync(path);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenPathIsEmpty_ReturnsTrue()
    {
        var issuerNameService = new TestIssuerNameService("https://example.com");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);
        var path = string.Empty;

        var result = await subject.ValidateAsync(path);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenPathIsNull_ReturnsTrue()
    {
        var issuerNameService = new TestIssuerNameService("https://example.com");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);

        var result = await subject.ValidateAsync(null);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenPathIsDifferentCasingThanIssuer_ReturnsTrue()
    {
        var issuerNameService = new TestIssuerNameService("https://example.com/FOO");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);
        var path = "/foo";

        var result = await subject.ValidateAsync(path);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerHasPortAndPathIsMatch_ReturnsTrue()
    {
        var issuerNameService = new TestIssuerNameService("https://example.com:5001/foo");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);
        var path = "/foo";

        var result = await subject.ValidateAsync(path);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerHasMultipleSegmentsAndPathIsMatch_ReturnsTrue()
    {
        var issuerNameService = new TestIssuerNameService("https://example.com/foo/bar");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);
        var path = "/foo/bar";

        var result = await subject.ValidateAsync(path);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerIsNotValidUri_ReturnsFalse()
    {
        var issuerNameService = new TestIssuerNameService("not-a-valid-uri");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);
        var path = "/foo";

        var result = await subject.ValidateAsync(path);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerPathDoesNotMatch_ReturnsFalse()
    {
        var issuerNameService = new TestIssuerNameService("https://example.com/bar");
        var logger = new FakeLogger<DefaultIssuerPathValidator>();
        var subject = new DefaultIssuerPathValidator(issuerNameService, logger);
        var path = "/foo";

        var result = await subject.ValidateAsync(path);

        result.ShouldBeFalse();
    }
}

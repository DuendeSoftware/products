// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace UnitTests.Saml;

/// <summary>
/// Security tests verifying that XmlException details are not leaked into SAML error responses.
/// </summary>
public class SamlRequestSignatureValidatorTests
{
    private const string Category = "SAML Request Signature Validator";

    private static SamlSigninRequest CreateRequestWithOversizedXml()
    {
        // Create XML whose string representation exceeds SecureXmlParser.MaxMessageSize,
        // which causes SecureXmlParser.LoadXmlDocument to throw XmlException with a message
        // that contains internal detail such as byte counts and size limits.
        var largeContent = new string('X', SecureXmlParser.MaxMessageSize + 1);
        var xmlDoc = new XDocument(new XElement("root", largeContent));

        return new SamlSigninRequest
        {
            Request = new AuthNRequest
            {
                Id = "_test123",
                Version = "2.0",
                IssueInstant = DateTime.UtcNow,
                Issuer = "https://sp.example.com"
            },
            RequestXml = xmlDoc,
            Binding = SamlBinding.HttpPost
        };
    }

    private static SamlServiceProvider CreateServiceProvider() =>
        new() { EntityId = "https://sp.example.com" };

    private static SamlRequestSignatureValidator<SamlSigninRequest, AuthNRequest> CreateValidator(
        FakeLogger<SamlRequestSignatureValidator<SamlSigninRequest, AuthNRequest>> logger) =>
        new(TimeProvider.System, logger);

    [Fact]
    [Trait("Category", Category)]
    public void ValidatePostBindingSignature_WithInvalidXml_ReturnsGenericErrorMessage()
    {
        // Arrange
        var logger = new FakeLogger<SamlRequestSignatureValidator<SamlSigninRequest, AuthNRequest>>();
        var validator = CreateValidator(logger);
        var request = CreateRequestWithOversizedXml();
        var sp = CreateServiceProvider();

        // Act
        var result = validator.ValidatePostBindingSignature(request, sp);

        // Assert — result is an error with a GENERIC message
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldBe("The SAML request contained invalid XML");

        // The XmlException detail must NOT appear in the SAML error response
        result.Error.Message.ShouldNotContain("exceeds maximum allowed size");
        result.Error.Message.ShouldNotContain("bytes");
        result.Error.Message.ShouldNotContain(SecureXmlParser.MaxMessageSize.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public void ValidatePostBindingSignature_WithInvalidXml_LogsExceptionAtWarning()
    {
        // Arrange
        var logger = new FakeLogger<SamlRequestSignatureValidator<SamlSigninRequest, AuthNRequest>>();
        var validator = CreateValidator(logger);
        var request = CreateRequestWithOversizedXml();
        var sp = CreateServiceProvider();

        // Act
        validator.ValidatePostBindingSignature(request, sp);

        // Assert — exception detail IS logged server-side (for diagnostics)
        var entries = logger.Collector.GetSnapshot();
        entries.Count.ShouldBe(1);

        var entry = entries[0];
        entry.Level.ShouldBe(LogLevel.Warning);

        // The exception object is captured in the log record
        entry.Exception.ShouldNotBeNull();
        entry.Exception.ShouldBeOfType<XmlException>();

        // The log message contains the internal detail (server-side only)
        entry.Message.ShouldContain("exceeds maximum allowed size");
    }
}

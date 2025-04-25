// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Validation.Secrets;

public class AttestationSecretParsing
{
    private const string Category = "Secrets - Attestation Secret Parsing";

    private readonly IdentityServerOptions _options;
    private readonly ILogger<AttestationSecretParser> _logger;
    private readonly AttestationSecretParser _parser;

    public AttestationSecretParsing()
    {
        _options = new IdentityServerOptions();
        _logger = new NullLogger<AttestationSecretParser>();

        _parser = new AttestationSecretParser(Options.Create(_options), _logger);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EmptyContext_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();

        var secret = await _parser.ParseAsync(context);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MissingClientId_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["OAuth-Client-Attestation"] = "attestationJwt";
        context.Request.Headers["OAuth-Client-Attestation-PoP"] = "popJwt";

        var secret = await _parser.ParseAsync(context);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ClientId_TooLong_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["OAuth-Client-Attestation"] = "attestationJwt";
        context.Request.Headers["OAuth-Client-Attestation-PoP"] = "popJwt";
        var longClientId = "x".Repeat(_options.InputLengthRestrictions.ClientId + 1);
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", longClientId }
            });

        var secret = await _parser.ParseAsync(context);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MissingAttestationHeader_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["OAuth-Client-Attestation-PoP"] = "popJwt";
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", "client_id" }
            });

        var secret = await _parser.ParseAsync(context);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MissingPopHeader_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["OAuth-Client-Attestation"] = "attestationJwt";
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", "client_id" }
            });

        var secret = await _parser.ParseAsync(context);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MultipleValuesInAttestationJWTHeader_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["OAuth-Client-Attestation"] = new[] { "attestationJwt", "attestationJwt" };
        context.Request.Headers["OAuth-Client-Attestation-PoP"] = "popJwt";
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", "client_id" }
            });

        var secret = await _parser.ParseAsync(context);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MultipleValuesInPopHeader_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["OAuth-Client-Attestation"] = "attestationJwt";
        context.Request.Headers["OAuth-Client-Attestation-PoP"] = new[] { "popJwt", "popJwt" };
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", "client_id" }
            });

        var secret = await _parser.ParseAsync(context);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SingleValueInEachHeader_ShouldReturnSecret()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["OAuth-Client-Attestation"] = "attestationJwt";
        context.Request.Headers["OAuth-Client-Attestation-PoP"] = "popJwt";
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", "client_id" }
            });

        var secret = await _parser.ParseAsync(context);

        secret.ShouldNotBeNull();
        secret.Id.ShouldBe("client_id");
        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.AttestationBased);
        secret.Credential.ShouldBeOfType<AttestationSecretValidationContext>();
        var attestationContext = (AttestationSecretValidationContext)secret.Credential;
        attestationContext.ClientId.ShouldBe("client_id");
        attestationContext.ClientAttestationJwt.ShouldBe("attestationJwt");
        attestationContext.ClientAttestationPopJwt.ShouldBe("popJwt");
    }

    [Theory]
    [InlineData("OAuth-Client-Attestation", "OAuth-Client-Attestation-PoP")]
    [InlineData("oAuth-Client-Attestation", "oAuth-Client-Attestation-PoP")]
    [InlineData("oAuTh-cLiEnT-AtTeStAtIoN", "oAuTh-cLiEnT-AtTeStAtIoN-PoP")]
    [InlineData("OAUTH-CLIENT-ATTESTATION", "OAUTH-CLIENT-ATTESTATION-POP")]
    [InlineData("oauth-client-attestation", "oauth-client-attestation-pop")]
    [Trait("Category", Category)]
    public async Task HeaderKeys_AreCaseInsensitive(string attestationHeaderKey, string popJwtKey)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[attestationHeaderKey] = "attestationJwt";
        context.Request.Headers[popJwtKey] = "popJwt";
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", "client_id" }
            });

        var secret = await _parser.ParseAsync(context);

        secret.ShouldNotBeNull();
        secret.Id.ShouldBe("client_id");
        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.AttestationBased);
        secret.Credential.ShouldBeOfType<AttestationSecretValidationContext>();
        var attestationContext = (AttestationSecretValidationContext)secret.Credential;
        attestationContext.ClientId.ShouldBe("client_id");
        attestationContext.ClientAttestationJwt.ShouldBe("attestationJwt");
        attestationContext.ClientAttestationPopJwt.ShouldBe("popJwt");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidConcatenatedFormat_ShouldReturnSecret()
    {
        var context = new DefaultHttpContext();
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "client_id", "client_id" }
            });
        context.Request.Headers.ContentType = "application/x-www-form-urlencoded";
        var formValues = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "OAuth-Client-Attestation", "attestationJwt~popJwt" }
        };
        context.Request.Form = new FormCollection(formValues);

        var secret = await _parser.ParseAsync(context);

        secret.ShouldNotBeNull();
        secret.Id.ShouldBe("client_id");
        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.AttestationBased);
        secret.Credential.ShouldBeOfType<AttestationSecretValidationContext>();
        var attestationContext = (AttestationSecretValidationContext)secret.Credential;
        attestationContext.ClientId.ShouldBe("client_id");
        attestationContext.ClientAttestationJwt.ShouldBe("attestationJwt");
        attestationContext.ClientAttestationPopJwt.ShouldBe("popJwt");
    }
}

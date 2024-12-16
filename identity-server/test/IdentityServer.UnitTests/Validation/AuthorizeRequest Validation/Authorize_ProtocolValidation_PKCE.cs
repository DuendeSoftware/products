// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using FluentAssertions;
using System.Collections.Specialized;
using System.Threading.Tasks;
using UnitTests.Common;
using UnitTests.Validation.Setup;
using Xunit;

namespace UnitTests.Validation.AuthorizeRequest_Validation;

public class Authorize_ProtocolValidation_Valid_PKCE
{
    private const string Category = "AuthorizeRequest Protocol Validation - PKCE";

    private InputLengthRestrictions lengths = new InputLengthRestrictions();

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task valid_openid_code_request_with_challenge_and_plain_method_should_be_forbidden_if_plain_is_forbidden(string clientId)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, clientId);
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "x".Repeat(lengths.CodeChallengeMinLength));
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Plain);
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(true);
        result.ErrorDescription.Should().Be("Transform algorithm not supported");
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task valid_openid_code_request_with_challenge_and_sh256_method_should_be_allowed(string clientId)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, clientId);
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "x".Repeat(lengths.CodeChallengeMinLength));
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Sha256);
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(false);
    }

    [Theory]
    [InlineData("codeclient.pkce.plain")]
    [InlineData("codeclient.plain")]
    [Trait("Category", Category)]
    public async Task valid_openid_code_request_with_challenge_and_missing_method_should_be_allowed_if_plain_is_allowed(string clientId)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, clientId);
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "x".Repeat(lengths.CodeChallengeMinLength));
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(false);
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task valid_openid_code_request_with_challenge_and_missing_method_should_be_forbidden_if_plain_is_forbidden(string clientId)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, clientId);
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "x".Repeat(lengths.CodeChallengeMinLength));
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(true);
        result.ErrorDescription.Should().Be("Transform algorithm not supported");
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task openid_code_request_missing_challenge_should_be_rejected()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient.pkce");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(true);
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        result.ErrorDescription.Should().Be("code challenge required");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task openid_hybrid_request_missing_challenge_should_be_rejected()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient.pkce");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(true);
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        result.ErrorDescription.Should().Be("code challenge required");
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient.pkce.plain")]
    [InlineData("codeclient")]
    [InlineData("codeclient.plain")]
    [Trait("Category", Category)]
    public async Task openid_code_request_with_challenge_and_invalid_method_should_be_rejected(string clientId)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, clientId);
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "x".Repeat(lengths.CodeChallengeMinLength));
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallengeMethod, "invalid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(true);
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        result.ErrorDescription.Should().Be("Transform algorithm not supported");
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient.pkce.plain")]
    [InlineData("codeclient")]
    [InlineData("codeclient.plain")]
    [Trait("Category", Category)]
    public async Task openid_code_request_with_too_short_challenge_should_be_rejected(string clientId)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, clientId);
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "x".Repeat(lengths.CodeChallengeMinLength - 1));
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Plain);
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(true);
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient.pkce.plain")]
    [InlineData("codeclient")]
    [InlineData("codeclient.plain")]
    [Trait("Category", Category)]
    public async Task openid_code_request_with_too_long_challenge_should_be_rejected(string clientId)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, clientId);
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallenge, "x".Repeat(lengths.CodeChallengeMaxLength + 1));
        parameters.Add(OidcConstants.AuthorizeRequest.CodeChallengeMethod, OidcConstants.CodeChallengeMethods.Plain);
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.Should().Be(true);
        result.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
    }
}

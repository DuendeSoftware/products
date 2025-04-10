// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.AuthorizeRequest_Validation;

public class Authorize_ProtocolValidation_Valid
{
    private const string Category = "AuthorizeRequest Protocol Validation - Valid";

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_Code_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Resource_Code_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Mixed_Code_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Resource_Token_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Token);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_IdToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdToken);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Mixed_IdTokenToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdTokenToken);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_IdToken_With_FormPost_ResponseMode_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, IdentityServerConstants.StandardScopes.OpenId);
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdToken);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.FormPost);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_IdToken_Token_With_FormPost_ResponseMode_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdTokenToken);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.FormPost);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_Code_Token_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_ResponseMode_For_Code_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Fragment);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task anonymous_user_should_produce_session_state_value()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Fragment);
        parameters.Add(OidcConstants.AuthorizeRequest.Prompt, OidcConstants.PromptModes.None);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.ValidatedRequest.SessionId.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multiple_prompt_values_should_be_accepted()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Fragment);
        parameters.Add(OidcConstants.AuthorizeRequest.Prompt, OidcConstants.PromptModes.Consent + ' ' + OidcConstants.PromptModes.Login);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.ValidatedRequest.PromptModes.Count().ShouldBe(2);
        result.ValidatedRequest.PromptModes.ShouldContain(OidcConstants.PromptModes.Login);
        result.ValidatedRequest.PromptModes.ShouldContain(OidcConstants.PromptModes.Consent);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task processed_prompt_values_should_overwrite_original_values()
    {
        var validator = Factory.CreateAuthorizeRequestValidator();

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Fragment);

        {
            parameters[OidcConstants.AuthorizeRequest.Prompt] = "consent login";
            var result = await validator.ValidateAsync(parameters);
            result.ValidatedRequest.PromptModes.ShouldBe([OidcConstants.PromptModes.Consent, OidcConstants.PromptModes.Login]);
        }
        {
            parameters[OidcConstants.AuthorizeRequest.Prompt] = "consent login";
            parameters[Constants.ProcessedPrompt] = "login";
            var result = await validator.ValidateAsync(parameters);
            result.ValidatedRequest.PromptModes.ShouldBe([OidcConstants.PromptModes.Consent]);
            result.ValidatedRequest.OriginalPromptModes.ShouldBe([OidcConstants.PromptModes.Consent, OidcConstants.PromptModes.Login]);
            result.ValidatedRequest.ProcessedPromptModes.ShouldBe([OidcConstants.PromptModes.Login]);
        }
        {
            parameters[OidcConstants.AuthorizeRequest.Prompt] = "consent login";
            parameters[Constants.ProcessedPrompt] = "login consent";
            var result = await validator.ValidateAsync(parameters);
            result.ValidatedRequest.PromptModes.ShouldBeEmpty();
            result.ValidatedRequest.OriginalPromptModes.ShouldBe([OidcConstants.PromptModes.Consent, OidcConstants.PromptModes.Login]);
            result.ValidatedRequest.ProcessedPromptModes.ShouldBe([OidcConstants.PromptModes.Consent, OidcConstants.PromptModes.Login], true);
        }
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.AuthorizeRequest_Validation;

public class Authorize_ProtocolValidation_Invalid
{
    private const string Category = "AuthorizeRequest Protocol Validation";

    [Fact]
    [Trait("Category", Category)]
    public async Task Null_Parameter()
    {
        var validator = Factory.CreateAuthorizeRequestValidator();

        Func<Task> act = () => validator.ValidateAsync(null);

        await act.ShouldThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Empty_Parameters()
    {
        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(new NameValueCollection());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    // fails because openid scope is requested, but no response type that indicates an identity token
    [Fact]
    [Trait("Category", Category)]
    public async Task OpenId_Token_Only_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, IdentityServerConstants.StandardScopes.OpenId);
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Token);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Resource_Only_IdToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdToken);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Mixed_Token_Only_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Token);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task OpenId_IdToken_Request_Nonce_Missing()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_ClientId()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/callback");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_Scope()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_RedirectUri()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("/malformed")]
    [Trait("Category", Category)]
    public async Task Malformed_RedirectUri(string redirectUri)
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, redirectUri);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Malformed_RedirectUri_Triple_Slash()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "client");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https:///attacker.com");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unknown_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, "unknown");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.UnsupportedResponseType);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResponseMode_For_IdToken_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdToken);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Query);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResponseMode_For_IdTokenToken_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdTokenToken);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Query);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResponseMode_For_CodeToken_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeToken);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Query);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResponseMode_For_CodeIdToken_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdToken);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Query);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResponseMode_For_CodeIdTokenToken_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdTokenToken);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Query);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Malformed_MaxAge()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.MaxAge, "malformed");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Negative_MaxAge()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.MaxAge, "-1");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResponseMode_For_TokenIdToken_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, "token id_token"); // Unconventional order
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Query);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_ResponseMode_For_IdTokenCodeToken_ResponseType()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, "id_token code token"); // Unconventional ordering
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Query);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_none_and_other_values_should_fail()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Fragment);
        parameters.Add(OidcConstants.AuthorizeRequest.Prompt, "none login");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }

    // The next three tests verify that when response_mode is explicitly requested and the
    // request later fails a grant-type or PKCE check, the error result carries the client's
    // requested response_mode rather than the flow default (regression for products#1554).

    [Fact]
    [Trait("Category", Category)]
    public async Task Grant_Type_Not_Allowed_For_Client_Should_Preserve_Explicit_ResponseMode_Fragment()
    {
        // implicitclient only allows the implicit grant, so response_type=code triggers
        // the "grant type not allowed for client" error path.
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Fragment);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.ValidatedRequest.ResponseMode.ShouldBe(OidcConstants.ResponseModes.Fragment);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Pkce_Required_But_Missing_Should_Preserve_Explicit_ResponseMode_Fragment()
    {
        // codeclient.pkce requires PKCE; omitting code_challenge triggers the PKCE error path.
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient.pkce");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.Fragment);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.ValidatedRequest.ResponseMode.ShouldBe(OidcConstants.ResponseModes.Fragment);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Pkce_Required_But_Missing_Should_Preserve_Explicit_ResponseMode_FormPost()
    {
        // codeclient.pkce requires PKCE; omitting code_challenge triggers the PKCE error path.
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient.pkce");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseMode, OidcConstants.ResponseModes.FormPost);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.ValidatedRequest.ResponseMode.ShouldBe(OidcConstants.ResponseModes.FormPost);
    }
}

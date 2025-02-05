// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Threading.Tasks;
using Duende.IdentityServer.Configuration;
using Shouldly;
using Duende.IdentityModel;
using UnitTests.Common;
using UnitTests.Validation.Setup;
using Xunit;

namespace UnitTests.Validation.AuthorizeRequest_Validation;

public class Authorize_ClientValidation_Valid
{
    private const string Category = "AuthorizeRequest Client Validation - Valid";

    private IdentityServerOptions _options = TestIdentityServerOptions.Create();

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
            
        result.IsError.ShouldBeFalse();
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
    public async Task Valid_Mixed_Code_Request_Multiple_Scopes()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "codeclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid profile resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Code);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);
            
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_CodeIdToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "nonce");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_CodeIdTokenToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "nonce");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdTokenToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Mixed_CodeIdToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "nonce");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Mixed_CodeIdTokenToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "nonce");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdTokenToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_OpenId_IdTokenToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdTokenToken);
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
    public async Task Valid_Mixed_IdTokenToken_Request_Multiple_Scopes()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid profile resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdTokenToken);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

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
    [Trait("Category", "AuthorizeRequest Client Validation - Valid")]
    public async Task Valid_OpenId_TokenIdToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, "token id_token"); // Unconventional order
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeFalse();
    }
}
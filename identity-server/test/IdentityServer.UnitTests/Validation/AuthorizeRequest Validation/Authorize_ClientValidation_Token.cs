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

public class Authorize_ClientValidation_Token
{
    private const string Category = "AuthorizeRequest Client Validation - Token";

    private IdentityServerOptions _options = TestIdentityServerOptions.Create();

    [Fact]
    [Trait("Category", Category)]
    public async Task Mixed_Token_Request_Without_OpenId_Scope()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "resource profile");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.Token);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);
            
        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IdTokenToken_Request_with_no_AAVB()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient_no_aavb");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdTokenToken);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task CodeIdTokenToken_Request_with_no_AAVB()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "hybridclient_no_aavb");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "nonce");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.CodeIdTokenToken);

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidRequest);
    }
}
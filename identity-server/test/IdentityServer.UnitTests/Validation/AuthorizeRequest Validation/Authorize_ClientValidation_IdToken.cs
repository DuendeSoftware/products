// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.AuthorizeRequest_Validation;

public class Authorize_ClientValidation_IdToken
{
    private IdentityServerOptions _options = TestIdentityServerOptions.Create();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", "AuthorizeRequest Client Validation - IdToken")]
    public async Task Mixed_IdToken_Request()
    {
        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.AuthorizeRequest.ClientId, "implicitclient");
        parameters.Add(OidcConstants.AuthorizeRequest.Scope, "openid resource");
        parameters.Add(OidcConstants.AuthorizeRequest.RedirectUri, "oob://implicit/cb");
        parameters.Add(OidcConstants.AuthorizeRequest.ResponseType, OidcConstants.ResponseTypes.IdToken);
        parameters.Add(OidcConstants.AuthorizeRequest.Nonce, "abc");

        var validator = Factory.CreateAuthorizeRequestValidator();
        var result = await validator.ValidateAsync(parameters, _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.AuthorizeErrors.InvalidScope);
    }
}

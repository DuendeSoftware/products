// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.AuthorizeRequest_Validation;

public class Authorize_ProtocolValidation_Valid_PAR
{
    private const string Category = "AuthorizeRequest Protocol Validation - PAR";

    [Fact]
    [Trait("Category", Category)]
    public void par_should_bind_client_to_pushed_request()
    {
        var initiallyPushedClientId = "clientId1";
        var par = new DeserializedPushedAuthorizationRequest
        {
            ReferenceValue = Guid.NewGuid().ToString(),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            PushedParameters = new NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.ClientId, initiallyPushedClientId }
            }
        };
        var differentClientInAuthorizeRequest = "notClientId1";
        var request = new ValidatedAuthorizeRequest
        {
            ClientId = differentClientInAuthorizeRequest
        };

        var result = RequestObjectValidator.ValidatePushedAuthorizationBindingToClient(par, request);

        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.ErrorDescription.ShouldBe("invalid client for pushed authorization request");
    }

    [Fact]
    [Trait("Category", Category)]
    public void expired_par_requests_should_fail()
    {
        var authorizeRequest = new ValidatedAuthorizeRequest();
        var par = new DeserializedPushedAuthorizationRequest
        {
            ReferenceValue = Guid.NewGuid().ToString(),
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1),
            PushedParameters = new NameValueCollection()
        };

        var validator = Factory.CreateRequestObjectValidator();
        var result = RequestObjectValidator.ValidatePushedAuthorizationExpiration(par, authorizeRequest);

        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.ErrorDescription.ShouldBe("expired pushed authorization request");
    }
}

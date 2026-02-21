// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityServer.Validation;

namespace UnitTests.Endpoints.Authorize;

public class StubAuthorizeRequestValidator : IAuthorizeRequestValidator
{
    public AuthorizeRequestValidationResult Result { get; set; }

    public Task<AuthorizeRequestValidationResult> ValidateAsync(NameValueCollection parameters, CT ct, ClaimsPrincipal subject = null, AuthorizeRequestType authorizeRequestType = AuthorizeRequestType.Authorize)
    {
        Result.ValidatedRequest.Raw = parameters;
        return Task.FromResult(Result);
    }
}

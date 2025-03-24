// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityServer.Validation;

namespace UnitTests.Endpoints.EndSession;

internal class StubEndSessionRequestValidator : IEndSessionRequestValidator
{
    public EndSessionValidationResult EndSessionValidationResult { get; set; } = new EndSessionValidationResult();
    public EndSessionCallbackValidationResult EndSessionCallbackValidationResult { get; set; } = new EndSessionCallbackValidationResult();

    public Task<EndSessionValidationResult> ValidateAsync(NameValueCollection parameters, ClaimsPrincipal subject)
    {
        return Task.FromResult(EndSessionValidationResult);
    }

    public Task<EndSessionCallbackValidationResult> ValidateCallbackAsync(NameValueCollection parameters)
    {
        return Task.FromResult(EndSessionCallbackValidationResult);
    }
}

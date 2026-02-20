// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace IdentityServer.Endpoints.Token;

internal class StubTokenRequestValidator : ITokenRequestValidator
{
    public TokenRequestValidationResult Result { get; set; }

    public Task<TokenRequestValidationResult> ValidateRequestAsync(TokenRequestValidationContext context, CT _) => Task.FromResult(Result);
}

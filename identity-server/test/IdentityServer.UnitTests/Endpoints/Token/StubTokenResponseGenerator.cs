// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Validation;

namespace IdentityServer.Endpoints.Token;

internal class StubTokenResponseGenerator : ITokenResponseGenerator
{
    public TokenResponse Response { get; set; } = new TokenResponse();

    public Task<TokenResponse> ProcessAsync(TokenRequestValidationResult validationResult) => Task.FromResult(Response);
}

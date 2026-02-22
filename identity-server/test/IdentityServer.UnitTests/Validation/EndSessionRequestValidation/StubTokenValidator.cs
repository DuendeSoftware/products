// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.EndSessionRequestValidation;

public class StubTokenValidator : ITokenValidator
{
    public TokenValidationResult AccessTokenValidationResult { get; set; } = new TokenValidationResult();
    public TokenValidationResult IdentityTokenValidationResult { get; set; } = new TokenValidationResult();

    public Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string expectedScope, Ct ct) => Task.FromResult(AccessTokenValidationResult);

    public Task<TokenValidationResult> ValidateIdentityTokenAsync(string token, string clientId, bool validateLifetime, Ct ct) => Task.FromResult(IdentityTokenValidationResult);
}

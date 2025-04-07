// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.EndSessionRequestValidation;

public class StubTokenValidator : ITokenValidator
{
    public TokenValidationResult AccessTokenValidationResult { get; set; } = new TokenValidationResult();
    public TokenValidationResult IdentityTokenValidationResult { get; set; } = new TokenValidationResult();

    public Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string expectedScope = null) => Task.FromResult(AccessTokenValidationResult);

    public Task<TokenValidationResult> ValidateIdentityTokenAsync(string token, string clientId = null, bool validateLifetime = true) => Task.FromResult(IdentityTokenValidationResult);

    public Task<TokenValidationResult> ValidateRefreshTokenAsync(string token, Client client) => throw new System.NotImplementedException();
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.Setup;

internal class TestTokenValidator : ITokenValidator
{
    private readonly TokenValidationResult _result;

    public TestTokenValidator(TokenValidationResult result) => _result = result;

    public Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string expectedScope, CT ct) => Task.FromResult(_result);

    public Task<TokenValidationResult> ValidateIdentityTokenAsync(string token, string clientId, bool validateLifetime, CT ct) => Task.FromResult(_result);
}

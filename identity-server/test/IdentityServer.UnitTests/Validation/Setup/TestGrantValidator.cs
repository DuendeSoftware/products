// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.Setup;

internal class TestGrantValidator : IExtensionGrantValidator
{
    private readonly bool _isInvalid;
    private readonly string _errorDescription;

    public TestGrantValidator(bool isInvalid = false, string errorDescription = null)
    {
        _isInvalid = isInvalid;
        _errorDescription = errorDescription;
    }

    public Task<GrantValidationResult> ValidateAsync(ValidatedTokenRequest request)
    {
        if (_isInvalid)
        {
            return Task.FromResult(new GrantValidationResult(TokenRequestErrors.InvalidGrant, _errorDescription));
        }

        return Task.FromResult(new GrantValidationResult("bob", "CustomGrant"));
    }

    public Task ValidateAsync(ExtensionGrantValidationContext context, CT ct)
    {
        if (_isInvalid)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, _errorDescription);
        }
        else
        {
            context.Result = new GrantValidationResult("bob", "CustomGrant");
        }

        return Task.CompletedTask;
    }

    public string GrantType => "custom_grant";
}

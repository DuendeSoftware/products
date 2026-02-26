// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.Setup;

public class TestResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    private string _erroDescription;
    private TokenRequestErrors _error;
    private readonly bool _sendError;

    public TestResourceOwnerPasswordValidator()
    { }

    public TestResourceOwnerPasswordValidator(TokenRequestErrors error, string errorDescription = null)
    {
        _sendError = true;
        _error = error;
        _erroDescription = errorDescription;
    }

    public Task ValidateAsync(ResourceOwnerPasswordValidationContext context, Ct _)
    {
        if (_sendError)
        {
            context.Result = new GrantValidationResult(_error, _erroDescription);
            return Task.CompletedTask;
        }

        if (context.UserName == context.Password)
        {
            context.Result = new GrantValidationResult(context.UserName, "password");
        }

        if (context.UserName == "bob_no_password" && context.Password == "")
        {
            context.Result = new GrantValidationResult(context.UserName, "password");
        }

        return Task.CompletedTask;
    }
}

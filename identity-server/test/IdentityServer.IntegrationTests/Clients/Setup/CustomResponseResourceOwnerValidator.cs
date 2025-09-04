// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.IntegrationTests.Clients.Setup;

public class CustomResponseResourceOwnerValidator : IResourceOwnerPasswordValidator
{
    public Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        var response = new Dictionary<string, object>
        {
            { "string_value", "some_string" },
            { "int_value", 42 },
            { "dto",  CustomResponseDto.Create }
        };

        if (context.UserName == context.Password)
        {
            context.Result = new GrantValidationResult(context.UserName, "password", customResponse: response);
        }
        else
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "invalid_credential", response);
        }

        return Task.CompletedTask;
    }
}

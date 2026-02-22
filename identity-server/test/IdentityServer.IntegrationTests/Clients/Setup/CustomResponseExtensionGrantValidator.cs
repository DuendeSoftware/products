// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.IntegrationTests.Clients.Setup;

public class CustomResponseExtensionGrantValidator : IExtensionGrantValidator
{
    public Task ValidateAsync(ExtensionGrantValidationContext context, Ct ct)
    {
        var response = new Dictionary<string, object>
        {
            { "string_value", "some_string" },
            { "int_value", 42 },
            { "dto",  CustomResponseDto.Create }
        };

        var credential = context.Request.Raw.Get("outcome");

        if (credential == "succeed")
        {
            context.Result = new GrantValidationResult("bob", "custom", customResponse: response);
        }
        else
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "invalid_credential", response);
        }

        return Task.CompletedTask;
    }

    public string GrantType => "custom";
}

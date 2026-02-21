// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.IntegrationTests.Clients.Setup;

public class TestCustomTokenRequestValidator : ICustomTokenRequestValidator
{
    public Task ValidateAsync(CustomTokenRequestValidationContext context, CT ct)
    {
        context.Result.CustomResponse = new Dictionary<string, object>
        {
            {"custom", "custom" }
        };

        return Task.CompletedTask;
    }
}

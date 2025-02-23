// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Threading.Tasks;
using Duende.IdentityServer.Validation;

namespace IntegrationTests.Clients.Setup;

public class TestCustomTokenRequestValidator : ICustomTokenRequestValidator
{
    public Task ValidateAsync(CustomTokenRequestValidationContext context)
    {
        context.Result.CustomResponse = new Dictionary<string, object>
        {
            {"custom", "custom" }
        };

        return Task.CompletedTask;
    }
}
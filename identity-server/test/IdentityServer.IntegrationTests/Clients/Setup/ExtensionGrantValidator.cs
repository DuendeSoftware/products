// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.IntegrationTests.Clients.Setup;

public class ExtensionGrantValidator : IExtensionGrantValidator
{
    public Task ValidateAsync(ExtensionGrantValidationContext context, Ct ct)
    {
        var credential = context.Request.Raw.Get("custom_credential");
        var extraClaim = context.Request.Raw.Get("extra_claim");

        if (credential != null)
        {
            if (extraClaim != null)
            {
                context.Result = new GrantValidationResult(
                    subject: "818727",
                    claims: new[] { new Claim("extra_claim", extraClaim) },
                    authenticationMethod: GrantType);
            }
            else
            {
                context.Result = new GrantValidationResult(subject: "818727", authenticationMethod: GrantType);
            }
        }
        else
        {
            // custom error message
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "invalid_custom_credential");
        }

        return Task.CompletedTask;
    }

    public string GrantType => "custom";
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.IntegrationTests.Clients.Setup;

public class DynamicParameterExtensionGrantValidator : IExtensionGrantValidator
{
    public Task ValidateAsync(ExtensionGrantValidationContext context, CT ct)
    {
        var impersonatedClient = context.Request.Raw.Get("impersonated_client");
        var lifetime = context.Request.Raw.Get("lifetime");
        var extraClaim = context.Request.Raw.Get("claim");
        var tokenType = context.Request.Raw.Get("type");
        var sub = context.Request.Raw.Get("sub");

        if (!string.IsNullOrEmpty(impersonatedClient))
        {
            context.Request.ClientId = impersonatedClient;
        }

        if (!string.IsNullOrEmpty(lifetime))
        {
            context.Request.AccessTokenLifetime = int.Parse(lifetime);
        }

        if (!string.IsNullOrEmpty(tokenType))
        {
            if (tokenType == "jwt")
            {
                context.Request.AccessTokenType = AccessTokenType.Jwt;
            }
            else if (tokenType == "reference")
            {
                context.Request.AccessTokenType = AccessTokenType.Reference;
            }
        }

        if (!string.IsNullOrEmpty(extraClaim))
        {
            context.Request.ClientClaims.Add(new Claim("extra", extraClaim));
        }

        if (!string.IsNullOrEmpty(sub))
        {
            context.Result = new GrantValidationResult(sub, "delegation");
        }
        else
        {
            context.Result = new GrantValidationResult();
        }

        return Task.CompletedTask;
    }

    public string GrantType => "dynamic";
}

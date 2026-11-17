// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer.Configuration.Models;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Duende.IdentityServer.Configuration.Validation.DynamicClientRegistration;
using Duende.IdentityServer.Hosts.Shared.Configuration;

namespace Duende.IdentityServer.Conformance.Host;

/// <summary>
/// DCR validator for the conformance host. The OIDC logout plans (RP-initiated,
/// front-channel, back-channel, session management) register clients dynamically
/// and send plain authorization code requests without PKCE. PKCE is not required
/// by OpenID Connect Core, so PKCE is made optional for those clients.
/// FAPI 2.0 clients (private_key_jwt) keep the default of RequirePkce = true.
/// When a client sends a code_challenge, IdentityServer still validates PKCE.
/// </summary>
internal sealed class ConformanceDynamicClientRegistrationValidator(
    ILogger<DynamicClientRegistrationValidator> logger) : DynamicClientRegistrationValidator(logger)
{
    protected override async Task<IStepResult> SetPublicClientProperties(DynamicClientRegistrationContext context, Ct ct)
    {
        var result = await base.SetPublicClientProperties(context, ct);

        if (context.Request.TokenEndpointAuthenticationMethod != OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt)
        {
            context.Client.RequirePkce = false;
        }

        return result;
    }

    /// <summary>
    /// The OIDC logout plans register clients without a scope parameter.
    /// Allow the identity scopes so that their authorize requests succeed.
    /// </summary>
    protected override Task<IStepResult> SetDefaultScopes(DynamicClientRegistrationContext context, Ct ct)
    {
        foreach (var resource in TestResources.IdentityResources)
        {
            context.Client.AllowedScopes.Add(resource.Name);
        }

        return StepResult.Success();
    }
}

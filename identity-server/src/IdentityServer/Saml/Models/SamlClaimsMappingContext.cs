// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml.Models;

public record SamlClaimsMappingContext
{
    /// <summary>
    /// The claims issued for the current user to be mapped to SAML Attributes
    /// for inclusion in the SAMLResponse.
    /// </summary>
    public IEnumerable<Claim> UserClaims { get; init; } = [];

    /// <summary>
    /// The Service Provider which initiated the Authn request.
    /// </summary>
    public required SamlServiceProvider ServiceProvider { get; init; }
}

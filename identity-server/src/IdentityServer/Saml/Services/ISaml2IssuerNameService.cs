// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Services;

/// <summary>
/// Access to current IdentityServer Entity Id.
/// </summary>
public interface ISaml2IssuerNameService
{
    /// <summary>
    /// Get the current IdentityServer Entity Id
    /// </summary>
    /// <returns>Entity Id string</returns>
    Task<string> GetCurrentAsync(Ct ct);
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// Specifies a single identity provider, Core 3.4.1.3.1
/// </summary>
public class IdpEntry
{
    /// <summary>
    /// Unique identifier (the Entity Id) of the identity provider.
    /// </summary>
    public string ProviderId { get; set; } = default!;

    /// <summary>
    /// Human-readable name of the identity provider.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// URI reference representing the location of a profile-specific endpoint.
    /// </summary>
    public string? Loc { get; set; }
}

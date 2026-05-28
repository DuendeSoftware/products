// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// An advisory list of identity providers and associated information.
/// Element IdpList, Core 3.4.1.3
/// </summary>
public class IdpList
{
    /// <summary>
    /// Specifies a single identity provider.
    /// </summary>
    public List<IdpEntry> IdpEntries { get; } = [];

    /// <summary>
    /// If the IdpList is not complete, use URI reference.
    /// </summary>
    public string? GetComplete { get; set; }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Audience Restrictions, Core 2.5.1.4
/// </summary>
public class AudienceRestriction
{
    /// <summary>
    /// Audiences, a list of URIs identifying the audiences.
    /// </summary>
    public List<string> Audiences { get; } = [];
}

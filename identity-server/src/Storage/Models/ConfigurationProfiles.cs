// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Constants for well-known configuration profiles that IdentityServer can enforce.
/// Configuration profiles allow developers to express the intention that they are following a particular specification or profile,
/// such as OAuth 2.1, FAPI 2.0, etc. IdentityServer will automatically configure options and validate client configuration to
/// comply with the profile.
/// </summary>
public static class ConfigurationProfiles
{
    /// <summary>
    /// FAPI 2.0 Security Profile.
    /// When this profile is active, IdentityServer will enforce FAPI 2.0 requirements including:
    /// - Pushed Authorization Requests (PAR) are required
    /// - Sender-constrained tokens (DPoP if no other constraint is configured)
    /// - Other FAPI 2.0 security requirements
    /// </summary>
    public const string Fapi2 = "fapi2";

    /// <summary>
    /// OAuth 2.1 Profile.
    /// When this profile is active, IdentityServer will enforce OAuth 2.1 requirements including:
    /// - Pushed Authorization Requests (PAR) are required
    /// - PKCE is required for authorization code flow
    /// - Other OAuth 2.1 requirements
    /// </summary>
    public const string OAuth21 = "oauth21";
}

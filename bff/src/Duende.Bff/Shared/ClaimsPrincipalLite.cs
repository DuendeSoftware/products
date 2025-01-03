// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Serialization friendly ClaimsPrincipal
/// </summary>
internal class ClaimsPrincipalLite
{
    /// <summary>
    /// The authentication type
    /// </summary>
    public string? AuthenticationType { get; init; }

    /// <summary>
    /// The name claim type
    /// </summary>
    public string? NameClaimType { get; init; }

    /// <summary>
    /// The role claim type
    /// </summary>
    public string? RoleClaimType { get; init; }

    /// <summary>
    /// The claims
    /// </summary>
    public ClaimLite[] Claims { get; init; } = default!;
}
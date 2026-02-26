// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents SAML SP session data stored in the user's authentication session.
/// </summary>
/// <remarks>
/// <para>
/// IMPORTANT: For production deployments with multiple SAML service providers,
/// server-side sessions SHOULD be enabled to avoid cookie size limitations.
/// Configure with: builder.AddServerSideSessions()
/// </para>
/// <para>
/// Without server-side sessions, session data is stored in the authentication cookie.
/// Practical limits are approximately:
/// - 5-8 SAML SPs with 5 OIDC clients
/// - 3-5 SAML SPs with 10+ OIDC clients
/// Browser cookie size limit is ~4KB; exceeding this causes cookie chunking and performance degradation.
/// </para>
/// <para>
/// With server-side sessions enabled, there is no practical limit on the number of SAML sessions.
/// </para>
/// </remarks>
public class SamlSpSessionData
{
    /// <summary>
    /// Gets or sets the SAML Service Provider's EntityId.
    /// </summary>
    public string EntityId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the SAML SessionIndex value for this SP session.
    /// This value is unique per SP and is included in the SAML AuthnStatement.
    /// </summary>
    public string SessionIndex { get; set; } = default!;

    /// <summary>
    /// Gets or sets the NameID value sent to the SP.
    /// </summary>
    public string NameId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the NameID Format used for this SP.
    /// </summary>
    public string? NameIdFormat { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// Two SamlSpSessionData instances are considered equal if they have the same EntityId and SessionIndex,
    /// as these uniquely identify a SAML session at a specific Service Provider.
    /// </summary>
    public override bool Equals(object? obj) => obj is SamlSpSessionData other &&
               EntityId == other.EntityId &&
               SessionIndex == other.SessionIndex;

    /// <summary>
    /// Returns a hash code for this instance based on EntityId and SessionIndex.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(EntityId, SessionIndex);
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Manages the persistence of SAML logout session tracking state.
/// </summary>
/// <remarks>
/// Implementations track which SPs have been sent LogoutRequests and correlate
/// incoming LogoutResponses (via <c>InResponseTo</c>) to determine whether all
/// SPs successfully logged out.
/// </remarks>
public interface ISamlLogoutSessionStore
{
    /// <summary>
    /// Stores a new logout session.
    /// </summary>
    /// <param name="session">The session to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StoreAsync(SamlLogoutSession session, Ct ct);

    /// <summary>
    /// Retrieves a logout session by its logout ID. Returns <see langword="null"/>
    /// if not found or expired.
    /// </summary>
    /// <param name="logoutId">The logout ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SamlLogoutSession?> GetByLogoutIdAsync(string logoutId, Ct ct);

    /// <summary>
    /// Records a LogoutResponse for a previously stored request. Looks up the session
    /// by request ID (secondary index), verifies the issuer matches the expected SP entity ID,
    /// and records the response.
    /// </summary>
    /// <param name="requestId">The <c>InResponseTo</c> value from the LogoutResponse.</param>
    /// <param name="issuer">The issuer of the LogoutResponse (SP entity ID).</param>
    /// <param name="success">Whether the response indicated successful logout.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the response was recorded; <see langword="false"/>
    /// if the request ID was not found or the issuer did not match.</returns>
    Task<bool> TryRecordResponseAsync(string requestId, string issuer, bool success, Ct ct);

    /// <summary>
    /// Removes a logout session. Idempotent — does not throw if the session
    /// does not exist.
    /// </summary>
    /// <param name="logoutId">The logout ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveAsync(string logoutId, Ct ct);
}

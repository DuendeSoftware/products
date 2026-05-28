// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Manages the persistence of SAML signin request state.
/// </summary>
/// <remarks>
/// State lifecycle is primarily managed through TTL-based expiry configured on the store
/// implementation. The callback endpoint intentionally retains state after a successful
/// response to allow retries (e.g., browser reload if the response doesn't reach the SP).
/// Explicit removal via <see cref="RemoveSigninRequestStateAsync"/> is available for
/// scenarios that require immediate cleanup but is not called in the default flow.
/// </remarks>
public interface ISamlSigninStateStore
{
    /// <summary>
    /// Stores SAML signin request state and returns a unique identifier for later retrieval.
    /// </summary>
    Task<Guid> StoreSigninRequestStateAsync(SamlAuthenticationState state, Ct ct = default);

    /// <summary>
    /// Retrieves stored SAML signin request state without removing it. Returns <see langword="null"/>
    /// if not found or expired.
    /// </summary>
    Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(Guid stateId, Ct ct = default);

    /// <summary>
    /// Updates previously stored SAML signin request state in place. The state entry identified by
    /// <paramref name="stateId"/> must already exist. Used to write denial information back to the
    /// state so the callback endpoint can generate an error response.
    /// </summary>
    Task UpdateSigninRequestStateAsync(Guid stateId, SamlAuthenticationState state, Ct ct = default);

    /// <summary>
    /// Removes previously stored SAML signin request state. Idempotent — does not throw if the
    /// state has already been removed or does not exist.
    /// </summary>
    /// <remarks>
    /// This method is not called in the default callback flow, which relies on TTL-based expiry.
    /// It is provided for custom implementations that need immediate cleanup.
    /// </remarks>
    Task RemoveSigninRequestStateAsync(Guid stateId, Ct ct = default);
}

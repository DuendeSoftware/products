// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves user consent decisions. When a user grants a client permission
/// to access specific scopes, IdentityServer can remember that decision so the user is
/// not prompted again on subsequent authorization requests. Consent records are stored
/// per subject/client pair and optionally expire based on the client's
/// <c>ConsentLifetime</c> setting. The default implementation is backed by
/// <see cref="IPersistedGrantStore"/>.
/// </summary>
public interface IUserConsentStore
{
    /// <summary>
    /// Persists a user consent decision, recording which scopes the user has granted to
    /// the client. If a consent record already exists for the same subject and client,
    /// it is replaced.
    /// </summary>
    /// <param name="consent">The consent record to store.</param>
    /// <param name="ct">The cancellation token.</param>
    Task StoreUserConsentAsync(Consent consent, Ct ct);

    /// <summary>
    /// Retrieves the stored consent decision for the specified subject and client.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the user.</param>
    /// <param name="clientId">The client identifier for which consent was granted.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="Consent"/> record for the specified subject and client, or
    /// <see langword="null"/> if no consent has been stored or the stored consent has
    /// expired.
    /// </returns>
    Task<Consent?> GetUserConsentAsync(string subjectId, string clientId, Ct ct);

    /// <summary>
    /// Removes the stored consent decision for the specified subject and client,
    /// effectively revoking the user's previously granted consent.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the user.</param>
    /// <param name="clientId">The client identifier whose consent should be revoked.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveUserConsentAsync(string subjectId, string clientId, Ct ct);
}

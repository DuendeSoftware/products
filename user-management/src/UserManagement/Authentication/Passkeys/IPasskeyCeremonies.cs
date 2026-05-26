// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Service for passkey registration and authentication ceremonies.
/// </summary>
public interface IPasskeyCeremonies
{
    /// <summary>
    /// Begins a passkey registration ceremony for the specified user.
    /// </summary>
    /// <param name="userSubjectId">The subject ID of the user registering a passkey.</param>
    /// <param name="userName">The user's account name (e.g., email or username).</param>
    /// <param name="userDisplayName">The user's display name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A registration session containing options to pass to the browser's WebAuthn API.</returns>
    Task<PasskeyRegistrationSession> BeginRegistrationAsync(
        UserSubjectId userSubjectId,
        string userName,
        string userDisplayName,
        Ct ct);

    /// <summary>
    /// Completes a passkey registration ceremony by validating the attestation response.
    /// </summary>
    /// <param name="request">The attestation response from the authenticator.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Success with credential data, or failure with error details.</returns>
    Task<PasskeyRegistrationCompleteResult> CompleteRegistrationAsync(
        PasskeyCompleteRegistrationRequest request,
        Ct ct);

    /// <summary>
    /// Begins a discoverable (usernameless) passkey authentication ceremony.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Success with authentication session, or failure with error details.</returns>
    Task<PasskeyAuthenticationBeginResult> BeginAuthenticationAsync(Ct ct);

    /// <summary>
    /// Begins a passkey authentication ceremony for the specified user.
    /// </summary>
    /// <param name="userSubjectId">The subject ID of the user.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Success with authentication session, or failure with error details.</returns>
    Task<PasskeyAuthenticationBeginResult> BeginAuthenticationAsync(
        UserSubjectId userSubjectId,
        Ct ct);

    /// <summary>
    /// Completes a passkey authentication ceremony by validating the assertion response.
    /// </summary>
    /// <param name="request">The assertion response from the authenticator.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Success with user info and updated sign count, or failure with error details.</returns>
    Task<PasskeyAuthenticationCompleteResult> CompleteAuthenticationAsync(
        PasskeyCompleteAuthenticationRequest request,
        Ct ct);
}

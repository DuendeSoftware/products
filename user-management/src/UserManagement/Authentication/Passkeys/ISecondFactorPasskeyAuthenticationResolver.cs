// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Resolves the user for second-factor authentication.
/// </summary>
/// <remarks>
/// Implement this interface to resolve the user that completed the first factor authentication which will participate
/// in the PassKey as 2nd Factor Authentication Ceremony.
/// </remarks>
public interface ISecondFactorPasskeyAuthenticationResolver
{
    /// <summary>
    /// Resolves the second-factor passkey authentication context for the current request.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="UserSubjectId"/> identifying the user that is partially authenticated.
    /// </returns>
    Task<UserSubjectId?> ResolveAsync(Ct ct);
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Context passed to <see cref="IAuthenticationAttemptPolicy"/> before credential verification.
/// </summary>
/// <param name="SubjectId">The subject whose authenticator is being evaluated.</param>
/// <param name="AttemptInfo">The current persisted attempt state for the authenticator.</param>
public sealed record AuthenticationAttemptContext(
    UserSubjectId SubjectId,
    AuthenticatorAttemptInfo AttemptInfo);

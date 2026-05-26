// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Result of beginning passkey authentication.
/// </summary>
public abstract record PasskeyAuthenticationBeginResult
{
    private PasskeyAuthenticationBeginResult() { }

    /// <summary>
    /// Authentication begin completed successfully.
    /// </summary>
    /// <param name="Session">The authentication session with options for the browser's WebAuthn API.</param>
    public sealed record Success(PasskeyAuthenticationSession Session) : PasskeyAuthenticationBeginResult;

    /// <summary>
    /// Authentication begin failed with an error.
    /// </summary>
    /// <param name="Error">The error code.</param>
    /// <param name="ErrorDescription">Human-readable error description.</param>
    public sealed record Failure(
        AuthenticationBeginError Error,
        string ErrorDescription) : PasskeyAuthenticationBeginResult;
}

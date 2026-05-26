// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Result of completing passkey registration.
/// </summary>
public abstract record PasskeyRegistrationCompleteResult
{
    private PasskeyRegistrationCompleteResult() { }

    /// <summary>
    /// Registration completed successfully.
    /// </summary>
    /// <param name="Credential">The credential data.</param>
    public sealed record Success(PasskeyCredentialData Credential) : PasskeyRegistrationCompleteResult;

    /// <summary>
    /// Registration failed with an error.
    /// </summary>
    /// <param name="Error">The error code.</param>
    /// <param name="ErrorDescription">Human-readable error description.</param>
    public sealed record Failure(
        RegistrationError Error,
        string ErrorDescription) : PasskeyRegistrationCompleteResult;
}

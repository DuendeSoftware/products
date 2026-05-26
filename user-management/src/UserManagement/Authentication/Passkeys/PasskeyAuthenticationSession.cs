// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Result of beginning a passkey authentication ceremony.
/// Contains all the options needed to pass to the browser's WebAuthn API.
/// </summary>
public sealed record PasskeyAuthenticationSession
{
    internal PasskeyAuthenticationSession(
        PasskeyAuthenticationChallengeId challengeId,
        PublicKeyCredentialRequestOptions options)
    {
        ChallengeId = challengeId.Uuid.Value;
        Options = options;
    }

    /// <summary>
    /// The ID to use when completing authentication.
    /// </summary>
    public Guid ChallengeId { get; }

    /// <summary>
    /// The options to pass to navigator.credentials.get().
    /// </summary>
    public PublicKeyCredentialRequestOptions Options { get; }
}

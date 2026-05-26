// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Internal domain entity for a passkey authentication challenge.
/// </summary>
internal sealed class PasskeyAuthenticationChallenge
{
    private PasskeyAuthenticationChallenge(
        PasskeyAuthenticationChallengeId id,
        string challenge,
        UserSubjectId? userSubjectId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Challenge = challenge;
        UserSubjectId = userSubjectId;
        CreatedAt = createdAt;
    }

    internal PasskeyAuthenticationChallengeId Id { get; }
    internal string Challenge { get; }
    internal UserSubjectId? UserSubjectId { get; }
    internal DateTimeOffset CreatedAt { get; }

    internal static PasskeyAuthenticationChallenge Create(
        string challenge,
        UserSubjectId userSubjectId,
        DateTimeOffset createdAt) => new(
        PasskeyAuthenticationChallengeId.New(),
        challenge,
        userSubjectId,
        createdAt);

    internal static PasskeyAuthenticationChallenge CreateDiscoverable(
        string challenge,
        DateTimeOffset createdAt) => new(
        PasskeyAuthenticationChallengeId.New(),
        challenge,
        userSubjectId: null,
        createdAt);

    internal static PasskeyAuthenticationChallenge Load(
        PasskeyAuthenticationChallengeId id,
        string challenge,
        UserSubjectId? userSubjectId,
        DateTimeOffset createdAt) => new(
        id,
        challenge,
        userSubjectId,
        createdAt);
}

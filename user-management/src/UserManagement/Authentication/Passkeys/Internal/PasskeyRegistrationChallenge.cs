// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Internal domain entity for a passkey registration challenge.
/// </summary>
internal sealed class PasskeyRegistrationChallenge
{
    private PasskeyRegistrationChallenge(
        PasskeyRegistrationChallengeId id,
        string challenge,
        UserSubjectId userSubjectId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Challenge = challenge;
        UserSubjectId = userSubjectId;
        CreatedAt = createdAt;
    }

    internal PasskeyRegistrationChallengeId Id { get; }
    internal string Challenge { get; }
    internal UserSubjectId UserSubjectId { get; }
    internal DateTimeOffset CreatedAt { get; }

    internal static PasskeyRegistrationChallenge Create(
        string challenge,
        UserSubjectId userSubjectId,
        DateTimeOffset createdAt) => new(
        PasskeyRegistrationChallengeId.New(),
        challenge,
        userSubjectId,
        createdAt);

    internal static PasskeyRegistrationChallenge Load(
        PasskeyRegistrationChallengeId id,
        string challenge,
        UserSubjectId userSubjectId,
        DateTimeOffset createdAt) => new(
        id,
        challenge,
        userSubjectId,
        createdAt);
}

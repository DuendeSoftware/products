// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

internal interface IPasskeyRegistrationChallengeStore
{
    Task<CreateResult> CreateAsync(PasskeyRegistrationChallenge registrationChallenge, Ct ct);

    Task<PasskeyRegistrationChallenge?> TryReadAsync(
        PasskeyRegistrationChallengeId registrationChallengeId,
        Ct ct);

    Task<DeleteResult> DeleteAsync(PasskeyRegistrationChallengeId registrationChallengeId, Ct ct);
}

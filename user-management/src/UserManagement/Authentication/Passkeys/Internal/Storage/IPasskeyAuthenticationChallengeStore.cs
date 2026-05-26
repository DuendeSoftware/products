// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

internal interface IPasskeyAuthenticationChallengeStore
{
    Task<CreateResult> CreateAsync(PasskeyAuthenticationChallenge authenticationChallenge, Ct ct);

    Task<PasskeyAuthenticationChallenge?> TryReadAsync(
        PasskeyAuthenticationChallengeId authenticationChallengeId,
        Ct ct);

    Task<DeleteResult> DeleteAsync(PasskeyAuthenticationChallengeId authenticationChallengeId, Ct ct);
}

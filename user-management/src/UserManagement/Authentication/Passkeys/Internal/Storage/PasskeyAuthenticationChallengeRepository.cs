// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal.Storage;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PasskeyAuthenticationChallengeRepository(
    IStoreFactory storeFactory,
    IOptions<UserAuthenticationOptions> options) : IPasskeyAuthenticationChallengeStore
{
    public async Task<CreateResult> CreateAsync(PasskeyAuthenticationChallenge challenge, Ct ct)
    {
        var store = storeFactory.GetStore();
        var expiration = Expiration.InRelative(options.Value.Passkeys.ChallengeTimeout);

        return await store.CreateAsync(
            challenge.Id.Uuid.Value,
            ToDso(challenge),
            [],
            [],
            expiration,
            [],
            ct);
    }

    public async Task<PasskeyAuthenticationChallenge?> TryReadAsync(
        PasskeyAuthenticationChallengeId challengeId, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(
            PasskeyAuthenticationChallengeDso.EntityType,
            challengeId.Uuid.Value,
            ct);

        if (!result.Found)
        {
            return null;
        }

        var challenge = ToEntity(result.Dso);

        return challenge;
    }

    public async Task<DeleteResult> DeleteAsync(PasskeyAuthenticationChallengeId challengeId, Ct ct)
    {
        var store = storeFactory.GetStore();
        return await store.DeleteAsync(
            PasskeyAuthenticationChallengeDso.EntityType,
            challengeId.Uuid.Value,
            [],
            ct);
    }

    private static PasskeyAuthenticationChallengeDso.V1 ToDso(PasskeyAuthenticationChallenge challenge) => new(
        challenge.Id.Uuid.Value,
        challenge.Challenge,
        challenge.UserSubjectId?.Value,
        challenge.CreatedAt);

    private static PasskeyAuthenticationChallenge ToEntity(IDataStorageObject value) =>
        value switch
        {
            PasskeyAuthenticationChallengeDso.V1 v1 => ToEntity(v1),
            _ => throw new InvalidOperationException($"Unexpected type: {value.GetType().Name}")
        };

    private static PasskeyAuthenticationChallenge ToEntity(PasskeyAuthenticationChallengeDso.V1 dso) =>
        PasskeyAuthenticationChallenge.Load(
            PasskeyAuthenticationChallengeId.From(dso.Id),
            dso.Challenge,
            dso.UserSubjectId is not null ? (UserSubjectId?)UserSubjectId.Load(dso.UserSubjectId) : null,
            dso.CreatedAt);
}

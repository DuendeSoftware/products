// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal.Storage;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PasskeyRegistrationChallengeRepository(
    IPartitionedStorageFactory partitionedStorageFactory,
    IOptions<UserAuthenticationOptions> options) : IPasskeyRegistrationChallengeStore
{
    public async Task<CreateResult> CreateAsync(PasskeyRegistrationChallenge challenge, Ct ct)
    {
        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        var expiration = Expiration.InRelative(options.Value.Passkeys.ChallengeTimeout);

        return await partitionedStorage.CreateAsync(
            challenge.Id.Uuid,
            ToDso(challenge),
            keys: [],
            searchFieldCollection: [],
            expiration,
            [],
            ct);
    }

    public async Task<PasskeyRegistrationChallenge?> TryReadAsync(
        PasskeyRegistrationChallengeId challengeId, Ct ct)
    {
        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);
        var result = await partitionedStorage.TryReadAsync(
            PasskeyRegistrationChallengeDso.EntityType,
            challengeId.Uuid,
            ct);

        if (!result.Found)
        {
            return null;
        }

        var challenge = ToEntity(result.Dso);

        return challenge;
    }

    public async Task<DeleteResult> DeleteAsync(PasskeyRegistrationChallengeId challengeId, Ct ct)
    {
        var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, ct);

        return await partitionedStorage.DeleteAsync(
            PasskeyRegistrationChallengeDso.EntityType,
            challengeId.Uuid,
            [],
            ct);
    }

    private static PasskeyRegistrationChallengeDso.V1 ToDso(PasskeyRegistrationChallenge challenge) => new(
        challenge.Id.Uuid.Value,
        challenge.Challenge,
        challenge.UserSubjectId.Value,
        challenge.CreatedAt);

    private static PasskeyRegistrationChallenge ToEntity(IDataStorageObject value) =>
        value switch
        {
            PasskeyRegistrationChallengeDso.V1 v1 => ToEntity(v1),
            _ => throw new InvalidOperationException($"Unexpected type: {value.GetType().Name}")
        };

    private static PasskeyRegistrationChallenge ToEntity(PasskeyRegistrationChallengeDso.V1 dso) =>
        PasskeyRegistrationChallenge.Load(
            PasskeyRegistrationChallengeId.From(dso.Id),
            dso.Challenge,
            UserSubjectId.Load(dso.UserSubjectId),
            dso.CreatedAt);
}

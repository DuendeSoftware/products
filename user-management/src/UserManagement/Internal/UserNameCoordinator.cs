// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Profiles.Internal.Storage;

namespace Duende.UserManagement.Internal;

/// <summary>
/// Shared logic for building batch operations that update or remove the UserName
/// across all registered aspect modules and the root UserDso.
/// </summary>
internal sealed class UserNameCoordinator(
    IStoreFactory storeFactory,
    UserRepository userRepository,
    UserAuthenticatorsRepository? authenticatorsRepo,
    UserProfileRepository? profileRepo)
{
    internal async Task<bool> TrySetUserNameAsync(UserSubjectId subjectId, UserName userName, Ct ct)
    {
        List<IStoreOperation> operations = [];
        List<UserDso.AspectRef> updatedAspectRefs = [];

        if (authenticatorsRepo is not null &&
            await authenticatorsRepo.TryReadAsync(subjectId, ct) is ({ } authenticators, var authenticatorsVersion))
        {
            authenticators.SetUserName(userName);
            operations.Add(authenticatorsRepo.UpdateAspectOnlyBatchOperation(authenticators, authenticatorsVersion));
            updatedAspectRefs.Add(UserAuthenticatorsRepository.GetAspectRef(authenticators, authenticatorsVersion + 1));
        }

        if (profileRepo is not null &&
            await profileRepo.TryReadAsync(subjectId, ct) is ({ } profile, var profileVersion))
        {
            profile.SetUserName(userName);
            operations.Add(await profileRepo.UpdateAspectOnlyBatchOperationAsync(profile, profileVersion, ct));
            updatedAspectRefs.Add(UserProfileRepository.GetAspectRef(profile, profileVersion + 1));
        }

        // Update or create the root UserDso with the new username
        operations.Add(BuildUserDsoOperation(subjectId, userName.Value, updatedAspectRefs,
            await userRepository.TryReadAsync(subjectId, ct)));

        return await ExecuteBatchAsync(operations, ct);
    }

    internal async Task<bool> TryRemoveUserNameAsync(UserSubjectId subjectId, Ct ct)
    {
        List<IStoreOperation> operations = [];
        List<UserDso.AspectRef> updatedAspectRefs = [];

        if (authenticatorsRepo is not null &&
            await authenticatorsRepo.TryReadAsync(subjectId, ct) is ({ } authenticators, var authenticatorsVersion))
        {
            authenticators.RemoveUserName();
            operations.Add(authenticatorsRepo.UpdateAspectOnlyBatchOperation(authenticators, authenticatorsVersion));
            updatedAspectRefs.Add(UserAuthenticatorsRepository.GetAspectRef(authenticators, authenticatorsVersion + 1));
        }

        if (profileRepo is not null &&
            await profileRepo.TryReadAsync(subjectId, ct) is ({ } profile, var profileVersion))
        {
            profile.RemoveUserName();
            operations.Add(await profileRepo.UpdateAspectOnlyBatchOperationAsync(profile, profileVersion, ct));
            updatedAspectRefs.Add(UserProfileRepository.GetAspectRef(profile, profileVersion + 1));
        }

        // Build a single UserDso update clearing the username
        if (await userRepository.TryReadAsync(subjectId, ct) is var (user, userVersion))
        {
            var updated = user with { UserName = null };
            foreach (var aspectRef in updatedAspectRefs)
            {
                updated = UserRepository.AddOrUpdateAspectRef(updated, aspectRef);
            }

            operations.Add(UserRepository.UpdateBatchOperation(updated, userVersion));
        }

        return await ExecuteBatchAsync(operations, ct);
    }

    private static IStoreOperation BuildUserDsoOperation(
        UserSubjectId subjectId,
        string? userName,
        List<UserDso.AspectRef> updatedAspectRefs,
        (UserDso.V1 User, int Version)? existingUser)
    {
        if (existingUser is var (user, userVersion))
        {
            var updated = user with { UserName = userName };
            foreach (var aspectRef in updatedAspectRefs)
            {
                updated = UserRepository.AddOrUpdateAspectRef(updated, aspectRef);
            }

            return UserRepository.UpdateBatchOperation(updated, userVersion);
        }

        return UserRepository.CreateBatchOperation(subjectId, userName is not null ? UserName.Create(userName) : (UserName?)null, updatedAspectRefs);
    }

    internal async Task<bool> ExecuteBatchAsync(List<IStoreOperation> operations, Ct ct)
    {
        if (operations.Count == 0)
        {
            return false;
        }

        var store = storeFactory.GetStore();
        var result = await store.ExecuteBatchAsync(operations, [], ct);
        return result.Success;
    }
}

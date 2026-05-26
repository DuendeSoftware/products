// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserSelfService(
    IStoreFactory storeFactory,
    UserRepository userRepository,
    ILogger<UserSelfService> logger,
    UserAuthenticatorsRepository? authenticatorsRepo = null,
    UserProfileRepository? profileRepo = null) : IUserSelfService
{
    private readonly UserNameCoordinator _batchBuilder = new(
        storeFactory, userRepository, authenticatorsRepo, profileRepo);

    public async Task<bool> TrySetUserNameAsync(UserSubjectId subjectId, UserName userName, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        logger.UserNameSetStarting(LogLevel.Debug, subjectId);
        var result = await _batchBuilder.TrySetUserNameAsync(subjectId, userName, ct);
        if (result)
        {
            logger.UserNameSetSucceeded(LogLevel.Information, subjectId);
        }
        else
        {
            logger.UserNameSetFailed(LogLevel.Warning, subjectId);
        }

        return result;
    }

    public async Task<bool> TryRemoveUserNameAsync(UserSubjectId subjectId, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        logger.UserNameRemoveStarting(LogLevel.Debug, subjectId);
        var result = await _batchBuilder.TryRemoveUserNameAsync(subjectId, ct);
        if (result)
        {
            logger.UserNameRemoveSucceeded(LogLevel.Information, subjectId);
        }
        else
        {
            logger.UserNameRemoveFailed(LogLevel.Warning, subjectId);
        }

        return result;
    }

    public async Task<bool> TryDeregisterAsync(UserSubjectId subjectId, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        logger.UserDeregisterStarting(LogLevel.Debug, subjectId);
        List<IStoreOperation> operations = [UserRepository.DeleteBatchOperation(subjectId)];

        if (authenticatorsRepo is not null)
        {
            operations.Add(UserAuthenticatorsRepository.DeleteBatchOperation(subjectId));
        }

        if (profileRepo is not null)
        {
            operations.Add(UserProfileRepository.DeleteBatchOperation(subjectId));
        }

        var result = await ExecuteBatchAsync(operations, ct);
        if (result)
        {
            logger.UserDeregisterSucceeded(LogLevel.Information, subjectId);
        }
        else
        {
            logger.UserDeregisterFailed(LogLevel.Warning, subjectId);
        }

        return result;
    }

    private async Task<bool> ExecuteBatchAsync(List<IStoreOperation> operations, Ct ct)
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

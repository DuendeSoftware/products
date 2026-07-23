// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserSelfService(
    IStoreFactory storeFactory,
    ILogger<UserSelfService> logger,
    IUserProfileSelfService profileSelfService,
    IUserAuthenticatorsSelfService authenticatorsSelfService,
    UserManagementLicenseValidator licenseValidator,
    UserAuthenticatorsRepository? authenticatorsRepo = null,
    UserProfileRepository? profileRepo = null) : IUserSelfService
{
    public async Task<bool> TryDeleteAsync(UserSubjectId subjectId, Ct ct)
    {
        if (!licenseValidator.ValidateSelfService())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Self-Service feature.");
        }
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

    public IUserProfileSelfService Profiles => profileSelfService;
    public IUserAuthenticatorsSelfService Authenticators => authenticatorsSelfService;

    private async Task<bool> ExecuteBatchAsync(List<IStoreOperation> operations, Ct ct)
    {
        if (operations.Count == 0)
        {
            return false;
        }

        var store = await storeFactory.GetStore(ct);
        var result = await store.ExecuteBatchAsync(operations, [], ct);
        return result.Success;
    }
}

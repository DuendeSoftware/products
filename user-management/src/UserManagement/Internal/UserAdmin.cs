// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserAdmin(
    IStoreFactory storeFactory,
    ILogger<UserAdmin> logger,
    IUserProfileAdmin profileAdmin,
    IMembershipAdmin membershipAdmin,
    IUserAuthenticatorsAdmin authenticatorsAdmin,
    UserManagementLicenseValidator licenseValidator,
    UserAuthenticatorsRepository? authenticatorsRepo = null,
    UserProfileRepository? profileRepo = null) : IUserAdmin
{
    public async Task<bool> TryRemoveAsync(UserSubjectId subjectId, Ct ct)
    {
        licenseValidator.ValidateAdministration();
        using var scope = logger.BeginSubjectScope(subjectId);
        logger.UserDeleteStarting(LogLevel.Debug, subjectId);
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
            licenseValidator.ValidateUserCount();
            logger.UserDeleteSucceeded(LogLevel.Information, subjectId);
        }
        else
        {
            logger.UserDeleteFailed(LogLevel.Warning, subjectId);
        }

        return result;
    }

    public IMembershipAdmin Membership => membershipAdmin;

    public IUserProfileAdmin Profiles => profileAdmin;

    public IUserAuthenticatorsAdmin Authenticators => authenticatorsAdmin;

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

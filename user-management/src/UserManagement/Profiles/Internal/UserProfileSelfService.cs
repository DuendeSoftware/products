// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Profiles.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserProfileSelfService(UserProfileRepository repo, ISchemaStore schemaStore, ILogger<UserProfileSelfService> logger, UserManagementLicenseValidator licenseValidator)
    : IUserProfileSelfService
{
    public Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct) => schemaStore.GetAsync(SchemaId.UserProfile, ct);

    public async Task<Profiles.UserProfile?> TryCreateAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct)
    {
        if (!licenseValidator.ValidateProfiles())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Profiles feature.");
        }

        var profile = new UserProfile(subjectId, attributes);
        if (await repo.CreateAsync(profile, ct) is CreateResult.Success)
        {
            licenseValidator.ValidateUserCount();
            logger.UserProfileRegistered(LogLevel.Debug, subjectId);
            return new Profiles.UserProfile(profile);
        }

        logger.UserProfileRegisterFailed(LogLevel.Debug, subjectId);
        return null;
    }

    public async Task<Profiles.UserProfile?> TryGetAsync(UserSubjectId subjectId, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is ({ } profile, _))
        {
            logger.UserProfileFound(LogLevel.Debug, subjectId);
            return new Profiles.UserProfile(profile);
        }

        logger.UserProfileNotFound(LogLevel.Debug, subjectId);
        return null;
    }

    public async Task<Profiles.UserProfile?> TryUpdateAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct)
    {
        if (!licenseValidator.ValidateProfiles())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Profiles feature.");
        }

        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserProfileNotFound(LogLevel.Debug, subjectId);
            return null;
        }

        record.UserProfile.ReplaceAttributes(attributes);

        if (await repo.UpdateAsync(record.UserProfile, record.Version, ct) is UpdateResult.Success)
        {
            logger.UserProfileUpdated(LogLevel.Debug, subjectId);
            return new Profiles.UserProfile(record.UserProfile);
        }

        logger.UserProfileUpdateFailed(LogLevel.Debug, subjectId);
        return null;
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Profiles.Internal;

#pragma warning disable CS1573 // Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserProfileAdmin(UserProfileRepository repo, ISchemaStore schemaStore, ILogger<UserProfileAdmin> logger, UserManagementLicenseValidator licenseValidator) : IUserProfileAdmin
{
    public Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct) => schemaStore.GetAsync(SchemaId.UserProfile, ct);

    public async Task<Profiles.UserProfile?> TryAddAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct)
    {
        if (!licenseValidator.ValidateProfiles())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Profiles feature.");
        }

        var profile = new UserProfile(subjectId, await GetSchemaAsync(ct), attributes);
        if (await repo.CreateAsync(profile, ct) is CreateResult.Success)
        {
            licenseValidator.ValidateUserCount();
            logger.UserProfileCreated(LogLevel.Debug, subjectId);
            return new Profiles.UserProfile(profile);
        }

        logger.UserProfileCreateFailed(LogLevel.Debug, subjectId);
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

    public async Task<Profiles.UserProfile?> TryGetAsync(AttributeCode attributeCode, object value, Ct ct)
    {
        if (await repo.TryReadAsync(attributeCode, value, ct) is ({ } profile, _))
        {
            logger.UserProfileFoundByAttribute(LogLevel.Debug, attributeCode.Value);
            return new Profiles.UserProfile(profile);
        }

        logger.UserProfileNotFoundByAttribute(LogLevel.Debug, attributeCode.Value);
        return null;
    }

    public async Task<QueryResult<Profiles.UserProfile>> QueryAsync(QueryRequest request, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await StorageMapping.ExecuteQueryAsync(() => repo.QueryAsync(request.Filter?.ToStorage(), request.Sort?.ToStorage(), request.Range.ToStorage(), ct));
        return result.ConvertTo(profile => new Profiles.UserProfile(profile)).ToModel(request.Range);
    }

    public async Task<QueryResult<UserProfileAttributeProjection>> QueryAsync(
        QueryRequest request, HashSet<AttributeCode> attributes, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(attributes);

        var result = await StorageMapping.ExecuteQueryAsync(() => repo.QueryAsync(request.Filter?.ToStorage(), request.Sort?.ToStorage(), request.Range.ToStorage(), ct));
        return result.ConvertTo(profile => ProjectAttributes(profile, attributes)).ToModel(request.Range);
    }

    private static UserProfileAttributeProjection ProjectAttributes(UserProfile profile, HashSet<AttributeCode> attributes)
    {
        var values = profile.Attributes
            .Where(kvp => attributes.Contains(kvp.Key) && profile.Schema.AttributeDefinitions.ContainsKey(kvp.Key))
            .Select(kvp => kvp.Value);
        var collection = new AttributeValueCollection(profile.Schema, values);
        return new UserProfileAttributeProjection(profile.SubjectId, collection);
    }
}

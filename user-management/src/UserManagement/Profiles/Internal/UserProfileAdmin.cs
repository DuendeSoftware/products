// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Profiles.Internal;

#pragma warning disable CS1573 // Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserProfileAdmin(UserProfileRepository repo, AttributeSchemaRepository schemaRepo, ILogger<UserProfileAdmin> logger, UserManagementLicenseValidator licenseValidator) : IUserProfileAdmin
{
    public async Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct) =>
        await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct) is { } record ? record.AttributeSchema : AttributeSchema.Empty;

    public async Task<Profiles.UserProfile?> TryAddAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct)
    {
        licenseValidator.ValidateProfiles();
        var currentSchema = await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct) is { } r ? r.AttributeSchema : AttributeSchema.Empty;
        if (!SchemaFreshnessCheck.IsValid(attributes, currentSchema, logger))
        {
            return null;
        }

        var profile = new UserProfile(subjectId, attributes);
        if (await repo.CreateAsync(profile, ct) is CreateResult.Success)
        {
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

        var result = await repo.QueryAsync(request.Filter, request.Sort, request.Range, ct);
        return result.ConvertTo(profile => new Profiles.UserProfile(profile));
    }

    public async Task<QueryResult<UserProfileAttributeProjection>> QueryAsync(
        QueryRequest request, HashSet<AttributeCode> attributes, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(attributes);

        var result = await repo.QueryAsync(request.Filter, request.Sort, request.Range, ct);
        var schema = await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct) is { } r ? r.AttributeSchema : AttributeSchema.Empty;
        return result.ConvertTo(profile => ProjectAttributes(profile, attributes, schema));
    }

    private static UserProfileAttributeProjection ProjectAttributes(UserProfile profile, HashSet<AttributeCode> attributes, AttributeSchema schema)
    {
        var values = profile.Attributes
            .Where(kvp => attributes.Contains(kvp.Key) && schema.AttributeDefinitions.ContainsKey(kvp.Key))
            .Select(kvp => kvp.Value);
        var collection = new AttributeValueCollection(schema, values);
        return new UserProfileAttributeProjection(profile.SubjectId, profile.UserName, collection);
    }
}

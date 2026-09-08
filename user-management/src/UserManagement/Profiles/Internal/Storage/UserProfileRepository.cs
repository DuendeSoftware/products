// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Filtering;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Internal.Storage;

namespace Duende.UserManagement.Profiles.Internal.Storage;
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserProfileRepository(
    IStorageFactory storageFactory,
    ISchemaStore schemaStore,
    UserRepository userRepository)
{
    internal enum Keys
    {
        SubjectId = 1,
    }

    private Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct) => schemaStore.GetAsync(SchemaId.UserProfile, ct);

    internal async Task<CreateResult> CreateAsync(UserProfile profile, Ct ct)
    {
        var storage = await storageFactory.GetStorage(ct);
        var operations = await BuildCreateOperationsAsync(profile, ct);
        var result = await storage.ExecuteBatchAsync(operations, [], ct);
        if (result.Success)
        {
            return CreateResult.Success;
        }

        var firstFailure = result.Results.First(r => r.Outcome is not OperationOutcome.Success).Outcome;
        return firstFailure switch
        {
            OperationOutcome.KeyConflict => CreateResult.KeyConflict,
            OperationOutcome.AlreadyExists => CreateResult.AlreadyExists,
            _ => CreateResult.ConcurrencyConflict
        };
    }

    internal async Task<(UserProfile UserProfile, int Version)?> TryReadAsync(UserSubjectId subjectId, Ct ct)
    {
        var storage = await storageFactory.GetStorage(ct);
        var result = await storage.TryReadAsync(UserProfileDso.EntityType, DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)), ct);
        return result.Found
            ? (ToEntity(result.Dso, await GetSchemaAsync(ct)), result.Version.Value)
            : null;
    }

    /// <summary>
    /// Resolves a list of <see cref="UserSubjectId"/> values to their primary store UUIDs.
    /// Returns a dictionary of successfully resolved mappings and a list of subject IDs
    /// that could not be found.
    /// </summary>
    internal async Task<(Dictionary<UserSubjectId, UuidV7> Resolved, List<UserSubjectId> NotFound)>
        ResolveProfileUuidsAsync(IReadOnlyList<UserSubjectId> subjectIds, Ct ct)
    {
        var storage = await storageFactory.GetStorage(ct);
        var resolved = new Dictionary<UserSubjectId, UuidV7>(subjectIds.Count);
        var notFound = new List<UserSubjectId>();

        foreach (var subjectId in subjectIds)
        {
            var result = await storage.TryReadAsync(
                UserProfileDso.EntityType,
                DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)),
                ct);

            if (result.Found)
            {
                resolved[subjectId] = UuidV7.From(result.Id.Value);
            }
            else
            {
                notFound.Add(subjectId);
            }
        }

        return (resolved, notFound);
    }

    internal async Task<(UserProfile UserProfile, int Version)?> TryReadAsync(AttributeCode code, object value, Ct ct)
    {
        var storage = await storageFactory.GetStorage(ct);
        var result = await storage.TryReadAsync(UserProfileDso.EntityType, DataStorageKey.Create(AttributeValueDskV1.Create(code, value)), ct);
        return result.Found
            ? (ToEntity(result.Dso, await GetSchemaAsync(ct)), result.Version.Value)
            : null;
    }

    private static UserProfile ToEntity(IDataStorageObject value, IReadOnlyAttributeSchema? schema) =>
        value switch
        {
            UserProfileDso.V1 v1 => ToEntity(v1, schema),
            _ => throw new InvalidOperationException($"Unexpected type: {value.GetType().Name}")
        };

    internal async Task<UpdateResult> UpdateAsync(UserProfile profile, int expectedVersion, Ct ct)
    {
        var storage = await storageFactory.GetStorage(ct);
        var operations = await BuildUpdateOperationsAsync(profile, expectedVersion, ct);
        var result = await storage.ExecuteBatchAsync(operations, [], ct);
        if (result.Success)
        {
            return UpdateResult.Success;
        }

        // Find the first failed operation across the batch
        var firstFailure = result.Results.First(r => r.Outcome is not OperationOutcome.Success).Outcome;
        return firstFailure switch
        {
            OperationOutcome.KeyConflict => UpdateResult.KeyConflict,
            OperationOutcome.DoesNotExist => UpdateResult.DoesNotExist,
            _ => UpdateResult.UnexpectedVersion
        };
    }

    internal async Task<IReadOnlyList<IStorageOperation>> CreateBatchOperationAsync(UserProfile profile, Ct ct) =>
        await BuildCreateOperationsAsync(profile, ct);

    internal async Task<(CreateOperation AspectOp, UserDso.AspectRef AspectRef)> CreateAspectBatchOperationAsync(UserProfile profile, Ct ct)
    {
        var schema = await GetSchemaAsync(ct);
        var aspectOp = CreateOperation.For(
            profile.Id.Uuid,
            ToDso(profile),
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(profile.SubjectId)),
                .. GetJsonKeys(profile, schema)
            ],
            GetSearchFields(profile, schema),
            Expiration.NoExpiration);
        var aspectRef = new UserDso.AspectRef(profile.Id.Uuid.Value, 1, UserProfileDso.EntityType.Id);
        return (aspectOp, aspectRef);
    }

    internal static UserDso.AspectRef GetAspectRef(UserProfile profile, int version) =>
        new(profile.Id.Uuid.Value, version, UserProfileDso.EntityType.Id);

    internal async Task<IReadOnlyList<IStorageOperation>> UpdateBatchOperationAsync(UserProfile profile, int expectedVersion, Ct ct) =>
        await BuildUpdateOperationsAsync(profile, expectedVersion, ct);

    internal async Task<UpdateOperation> UpdateAspectOnlyBatchOperationAsync(UserProfile profile, int expectedVersion, Ct ct)
    {
        var schema = await GetSchemaAsync(ct);
        return UpdateOperation.For(
            profile.Id.Uuid,
            ToDso(profile),
            expectedVersion,
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(profile.SubjectId)),
                .. GetJsonKeys(profile, schema)
            ],
            GetSearchFields(profile, schema),
            Expiration.NoExpiration);
    }

    internal static DeleteOperation DeleteBatchOperation(UserSubjectId subjectId) =>
        DeleteOperation.ByKey(UserProfileDso.EntityType, DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)));

    internal async Task<QueryResult<UserProfile>> QueryAsync(
        FilterBy? filter, SortBy? sort, DataRange? range, Ct ct)
    {
        var queryStorage = await storageFactory.GetStorage(ct);
        var schema = await GetSchemaAsync(ct);
        var attributeDefinitions = schema?.AttributeDefinitions ??
                                   new Dictionary<AttributeCode, AttributeDefinition>();

        var queryFilter = BuildFilter(filter, attributeDefinitions);
        var sortParam = BuildSort(sort, attributeDefinitions);
        var dataRange = range ?? DataRange.FromPage(1, DataRangeSize.Default);
        if (dataRange.TokenValue is not null && sortParam.IsEmpty)
        {
            throw new NotSupportedException("User profile continuation-token pagination requires a valid sort.");
        }

        var result = await queryStorage.QueryAsync<UserProfileDso.V1>(
            UserProfileDso.EntityType,
            queryFilter,
            sortParam,
            dataRange,
            ct);

        return result.ConvertTo(envelope => ToEntity(envelope.Value, schema));
    }

    private async Task<List<IStorageOperation>> BuildCreateOperationsAsync(UserProfile profile, Ct ct)
    {
        var schema = await GetSchemaAsync(ct);

        var aspectRef = new UserDso.AspectRef(profile.Id.Uuid.Value, 1, UserProfileDso.EntityType.Id);
        var existingUser = await userRepository.TryReadAsync(profile.SubjectId, ct);

        var aspectOp = CreateOperation.For(
            profile.Id.Uuid,
            ToDso(profile),
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(profile.SubjectId)),
                .. GetJsonKeys(profile, schema)
            ],
            GetSearchFields(profile, schema),
            Expiration.NoExpiration);

        IStorageOperation userOp = existingUser is var (user, userVersion)
            ? UserRepository.UpdateBatchOperation(UserRepository.AddOrUpdateAspectRef(user, aspectRef), userVersion)
            : UserRepository.CreateBatchOperation(profile.SubjectId, [aspectRef]);

        return [userOp, aspectOp];
    }

    private async Task<List<IStorageOperation>> BuildUpdateOperationsAsync(UserProfile profile, int expectedVersion, Ct ct)
    {
        var schema = await GetSchemaAsync(ct);
        var aspectOp = UpdateOperation.For(
            profile.Id.Uuid,
            ToDso(profile),
            expectedVersion,
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(profile.SubjectId)),
                .. GetJsonKeys(profile, schema)
            ],
            GetSearchFields(profile, schema),
            Expiration.NoExpiration);

        var aspectRef = new UserDso.AspectRef(profile.Id.Uuid.Value, expectedVersion + 1, UserProfileDso.EntityType.Id);
        var existingUser = await userRepository.TryReadAsync(profile.SubjectId, ct);

        IStorageOperation userOp = existingUser is var (user, userVersion)
            ? UserRepository.UpdateBatchOperation(UserRepository.AddOrUpdateAspectRef(user, aspectRef), userVersion)
            : UserRepository.CreateBatchOperation(profile.SubjectId, [aspectRef]);

        return [userOp, aspectOp];
    }

    private static IQueryExpression BuildFilter(
        FilterBy? filter,
        IReadOnlyDictionary<AttributeCode, AttributeDefinition> attributeDefinitions)
    {
        if (filter?.SearchExpressionValue is not { } searchExpression)
        {
            return AllExpression.Instance;
        }

        var filterText = searchExpression.Value;
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return AllExpression.Instance;
        }

        var resolver = new AttributeTypeResolver(attributeDefinitions);
        var translator = new FilterTranslator(resolver);
        var translated = translator.Translate(filterText);

        return translated is not null
            ? Query.Where(translated)
            : AllExpression.Instance;
    }

    private static SortParameter BuildSort(
        SortBy? sort,
        IReadOnlyDictionary<AttributeCode, AttributeDefinition> attributeDefinitions)
    {
        if (sort is not SortBy.SortByAttributeCode attributeSort)
        {
            return SortParameter.Empty;
        }

        var resolver = new AttributeTypeResolver(attributeDefinitions);
        try
        {
            var field = resolver.ResolveField(attributeSort.Code.Value);
            return new SortParameter(field, attributeSort.Direction);
        }
        catch (NotSupportedException)
        {
            return SortParameter.Empty;
        }
    }

    private static UserProfileDso.V1 ToDso(UserProfile entity) => new(
        entity.Id.Uuid.Value,
        entity.SubjectId.Value,
        EavMapper.ToDsoList(entity.Attributes.Values));

    private static UserProfile ToEntity(UserProfileDso.V1 dso, IReadOnlyAttributeSchema? schema) =>
        UserProfile.Load(
            UserProfileId.Load(dso.Id),
            UserSubjectId.Load(dso.SubjectId),
            EavMapper.ToAttributeValues(dso.Attributes, schema));

    private static List<DataStorageKey> GetJsonKeys(UserProfile profile, IReadOnlyAttributeSchema? schema)
    {
        List<DataStorageKey> keys = [];

        if (schema is not null)
        {
            foreach (var attribute in profile.Attributes.Values)
            {
                if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition))
                {
                    continue;
                }

                if (!definition.IsUnique)
                {
                    continue;
                }

                keys.Add(DataStorageKey.Create(AttributeValueDskV1.Create(attribute)));
            }
        }

        return keys;
    }

    private static SearchFieldCollection GetSearchFields(UserProfile profile, IReadOnlyAttributeSchema? schema)
    {
        var builder = new SearchFieldsBuilder();

        if (schema is null)
        {
            return builder.Build();
        }

        foreach (var attribute in profile.Attributes.Values)
        {
            if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition))
            {
                continue;
            }

            if (!definition.IsQueryable)
            {
                continue;
            }

            AddSearchFieldsForType(builder, attribute.Code.Value, definition.AttributeType, attribute.UntypedValue, itemIndex: null);
        }

        return builder.Build();
    }

    private static void AddSearchFieldsForType(
        SearchFieldsBuilder builder, string fieldPath, AttributeType type, object value, int? itemIndex)
    {
        switch (type)
        {
            case ScalarAttributeType scalar:
                AddScalarSearchField(builder, fieldPath, scalar.DataType, value, itemIndex);
                break;
            case ComplexAttributeType complex when value is IReadOnlyDictionary<string, object> dict:
                foreach (var (propCode, prop) in complex.Properties)
                {
                    if (dict.TryGetValue(propCode.Value, out var propValue))
                    {
                        AddSearchFieldsForType(builder, $"{fieldPath}.{propCode.Value}", prop.Type, propValue, itemIndex);
                    }
                }
                break;
            case ListAttributeType list when value is IReadOnlyList<object> items:
                for (var i = 0; i < items.Count; i++)
                {
                    AddSearchFieldsForType(builder, fieldPath, list.ElementType, items[i], itemIndex: i);
                }
                break;
        }
    }

    private static void AddScalarSearchField(
        SearchFieldsBuilder builder, string fieldPath, ScalarDataType dataType, object value, int? itemIndex)
    {
        switch (dataType)
        {
            case ScalarDataType.Boolean when value is bool boolValue:
                _ = itemIndex.HasValue ? builder.Add(fieldPath, itemIndex.Value, boolValue) : builder.Add(fieldPath, boolValue);
                break;
            case ScalarDataType.Date when value is DateOnly dateValue:
                var dto = new DateTimeOffset(dateValue.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                _ = itemIndex.HasValue ? builder.Add(fieldPath, itemIndex.Value, dto) : builder.Add(fieldPath, dto);
                break;
            case ScalarDataType.DateTime when value is DateTimeOffset dateTimeValue:
                _ = itemIndex.HasValue ? builder.Add(fieldPath, itemIndex.Value, dateTimeValue) : builder.Add(fieldPath, dateTimeValue);
                break;
            case ScalarDataType.Decimal when value is decimal decimalValue:
                _ = itemIndex.HasValue ? builder.Add(fieldPath, itemIndex.Value, decimalValue) : builder.Add(fieldPath, decimalValue);
                break;
            case ScalarDataType.Integer when value is int intValue:
                _ = itemIndex.HasValue ? builder.Add(fieldPath, itemIndex.Value, intValue) : builder.Add(fieldPath, intValue);
                break;
            case ScalarDataType.String when value is string stringValue:
                _ = itemIndex.HasValue ? builder.Add(fieldPath, itemIndex.Value, stringValue) : builder.Add(fieldPath, stringValue);
                break;
        }
    }

}

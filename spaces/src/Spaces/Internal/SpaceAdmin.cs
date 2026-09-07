// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Querying;

namespace Duende.Spaces.Internal;

/// <summary>
/// Internal implementation of <see cref="ISpaceAdmin"/> that delegates to <see cref="SpaceRepository"/>.
/// </summary>
internal sealed class SpaceAdmin(SpaceRepository repository, ISchemaStore? schemaStore) : ISpaceAdmin
{
    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> CreateAsync(CreateSpaceConfiguration configuration, Ct ct)
    {
        if (configuration.PoolId is { } poolId && poolId.Value <= 0)
        {
            return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed(
                "Pool ID must be a positive integer. Pool 0 is reserved for the default space.", "PoolId"));
        }

        var validationError = ValidatePatterns(configuration.MatchPatterns);
        if (validationError is not null)
        {
            return SaveResult.Failure<SpaceId>(validationError);
        }

        var extendedProperties = configuration.ExtendedProperties ?? new AttributeValueCollection();
        var schemaError = await ValidateExtendedPropertiesAsync(extendedProperties, ct);
        if (schemaError is not null)
        {
            return SaveResult.Failure<SpaceId>(schemaError);
        }

        var uniquenessError = await CheckPatternUniquenessAsync(configuration.MatchPatterns, excludeSpaceId: null, ct);
        if (uniquenessError is not null)
        {
            return SaveResult.Failure<SpaceId>(uniquenessError);
        }

        return await repository.CreateAsync(configuration.Name, configuration.MatchPatterns, configuration.PoolId, extendedProperties, ct);
    }

    /// <inheritdoc/>
    public Task<GetResult<SpaceConfiguration>> GetAsync(SpaceId id, Ct ct) =>
        repository.GetByIdAsync(id, ct);

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> UpdateAsync(SpaceId id, SpaceConfiguration space, DataVersion expectedVersion, Ct ct)
    {
        var validationError = ValidatePatterns(space.MatchPatterns);
        if (validationError is not null)
        {
            return SaveResult.Failure<SpaceId>(validationError);
        }

        // PoolId is immutable after creation
        var existing = await repository.GetByIdAsync(id, ct);
        if (!existing.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
        }

        if (space.PoolId.Value != existing.Item.PoolId.Value)
        {
            return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed(
                "PoolId cannot be changed after creation.", "PoolId"));
        }

        var extendedProperties = EavMapper.ToMutableCollection(space.ExtendedProperties ?? []);
        var schemaError = await ValidateExtendedPropertiesAsync(extendedProperties, ct);
        if (schemaError is not null)
        {
            return SaveResult.Failure<SpaceId>(schemaError);
        }

        var uniquenessError = await CheckPatternUniquenessAsync(space.MatchPatterns, excludeSpaceId: id, ct);
        if (uniquenessError is not null)
        {
            return SaveResult.Failure<SpaceId>(uniquenessError);
        }

        return await repository.UpdateAsync(space, extendedProperties, expectedVersion.Value, ct);
    }

    /// <inheritdoc/>
    public Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, Ct ct) =>
        repository.DeleteAsync(id, ct);

    /// <inheritdoc/>
    public Task<SaveResult<SpaceId>> PurgeAsync(SpaceId id, Ct ct) =>
        repository.PurgeAsync(id, ct);

    /// <inheritdoc/>
    public Task<QueryResult<SpaceListItem>> QueryAsync(QueryRequest<SpaceFilter, SpaceSortField> request, Ct ct) =>
        repository.QueryAsync(request, ct);

    private async Task<StorageError?> ValidateExtendedPropertiesAsync(
        AttributeValueCollection extendedProperties,
        CancellationToken ct)
    {
        if (extendedProperties.Count == 0)
        {
            return null;
        }

        if (schemaStore is null)
        {
            return StorageError.ValidationFailed(
                "ExtendedProperties cannot be used: no schema store is configured. " +
                "Register a schema store to enable extended properties.",
                "ExtendedProperties");
        }

        var schema = await schemaStore.GetAsync(SchemaId.Space, ct);
        if (schema is null)
        {
            return StorageError.ValidationFailed(
                "ExtendedProperties cannot be used: no space schema is configured. " +
                "Register a schema via ISchemaStore to enable extended properties.",
                "ExtendedProperties");
        }

        if (!extendedProperties.TryValidateAgainst(schema, out var errors))
        {
            return StorageError.ValidationFailed(string.Join("; ", errors), "ExtendedProperties");
        }

        // Space extended properties only support scalar attribute types.
        // Reject any values that are not one of the supported scalar types.
        foreach (var attribute in extendedProperties)
        {
            if (attribute is not (AttributeValue<string> or AttributeValue<int> or AttributeValue<bool>
                or AttributeValue<decimal> or AttributeValue<DateOnly> or AttributeValue<DateTimeOffset>))
            {
                return StorageError.ValidationFailed(
                    $"Attribute '{attribute.Code}' has an unsupported type. " +
                    "Space extended properties only support scalar types (string, integer, boolean, decimal, date, datetime).",
                    "ExtendedProperties");
            }
        }

        return null;
    }

    private static StorageError? ValidatePatterns(IReadOnlyList<SpaceMatchPattern> patterns)
    {
        if (patterns.Count == 0)
        {
            return StorageError.ValidationFailed("At least one match pattern is required.", "MatchPatterns");
        }

        foreach (var pattern in patterns)
        {
            if (pattern.Origin is null && string.IsNullOrEmpty(pattern.Path))
            {
                return StorageError.ValidationFailed(
                    "Each match pattern must have at least one of Origin or Path set.", "MatchPatterns");
            }
        }

        return null;
    }

    private async Task<StorageError?> CheckPatternUniquenessAsync(
        IReadOnlyList<SpaceMatchPattern> patterns,
        SpaceId? excludeSpaceId,
        CancellationToken ct)
    {
        foreach (var pattern in patterns)
        {
            if (await repository.IsPatternRegisteredAsync(pattern, excludeSpaceId, ct))
            {
                return StorageError.AlreadyExists(
                    "match pattern",
                    $"Origin='{pattern.Origin}', Path='{pattern.Path}'",
                    "MatchPatterns");
            }
        }

        return null;
    }
}

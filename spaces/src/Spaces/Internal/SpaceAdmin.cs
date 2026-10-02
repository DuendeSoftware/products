// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Licensing;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Querying;

namespace Duende.Spaces.Internal;

/// <summary>
/// Internal implementation of <see cref="ISpaceAdmin"/> that delegates to <see cref="SpaceRepository"/>.
/// </summary>
internal sealed class SpaceAdmin(
    SpaceRepository repository,
    ISchemaStore? schemaStore,
    SpacesLicenseValidator licenseValidator) : ISpaceAdmin
{
    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> CreateAsync(CreateSpaceConfiguration configuration, Ct ct)
    {
        EnsureLicensed();

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

        var createResult = await repository.CreateAsync(configuration.Name, configuration.MatchPatterns,
            configuration.PoolId, extendedProperties, ct);
        if (createResult.IsSuccess)
        {
            await ValidateSpaceCountAsync(ct);
        }

        return createResult;
    }

    /// <inheritdoc/>
    public Task<GetResult<SpaceConfiguration>> GetAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();
        return repository.GetByIdRawAsync(id, ct);
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> UpdateAsync(SpaceId id, SpaceConfiguration space,
        DataVersion expectedVersion, Ct ct)
    {
        EnsureLicensed();

        var validationError = ValidatePatterns(space.MatchPatterns);
        if (validationError is not null)
        {
            return SaveResult.Failure<SpaceId>(validationError);
        }

        // PoolId is immutable via UpdateAsync
        var existing = await repository.GetByIdRawAsync(id, ct);
        if (!existing.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
        }

        if (space.PoolId.Value != existing.Item.PoolId.Value)
        {
            return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed(
                "PoolId cannot be changed after creation.", "PoolId"));
        }

        // A deleted space's match patterns remain reserved until purge (see IsPatternRegisteredAsync).
        // Allowing Update to change patterns on a deleted space could let it silently release a
        // reservation or (via pattern reuse) claim a new one, which would weaken that invariant.
        // Reject pattern changes explicitly instead of silently discarding them.
        //
        // This is a best-effort, fast-fail pre-check based on the state read above (which can go
        // stale if the space is concurrently deleted between this read and the repository's
        // persistence-time read). It exists purely to skip unnecessary schema/uniqueness work for
        // the common case; SpaceRepository.UpdateAsync independently re-derives IsDeleted from the
        // current row at persistence time and is the sole authority for enforcing the freeze.
        if (existing.Item.IsDeleted &&
            !SpaceMatchPatternPolicy.PatternsMatch(space.MatchPatterns, existing.Item.MatchPatterns))
        {
            return SaveResult.Failure<SpaceId>(StorageError.ValidationFailed(
                "Cannot change match patterns of a deleted space. Patterns remain reserved until the space is purged.",
                "MatchPatterns"));
        }

        var extendedProperties = EavMapper.ToMutableCollection(space.ExtendedProperties ?? []);
        var schemaError = await ValidateExtendedPropertiesAsync(extendedProperties, ct);
        if (schemaError is not null)
        {
            return SaveResult.Failure<SpaceId>(schemaError);
        }

        if (!existing.Item.IsDeleted)
        {
            var uniquenessError = await CheckPatternUniquenessAsync(space.MatchPatterns, excludeSpaceId: id, ct);
            if (uniquenessError is not null)
            {
                return SaveResult.Failure<SpaceId>(uniquenessError);
            }
        }

        return await repository.UpdateAsync(space, extendedProperties, expectedVersion.Value, ct);
    }

    /// <inheritdoc/>
    public Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();
        return repository.DeleteAsync(id, ct);
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> UndeleteAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();

        var result = await repository.UndeleteAsync(id, ct);
        if (result.IsSuccess)
        {
            await ValidateSpaceCountAsync(ct);
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<SaveResult<SpaceId>> PurgeAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();
        return repository.PurgeAsync(id, ct);
    }

    /// <inheritdoc/>
    public Task<QueryResult<SpaceListItem>> QueryAsync(QueryRequest<SpaceFilter, SpaceSortField> request, Ct ct)
    {
        EnsureLicensed();
        return repository.QueryAsync(request, ct);
    }

    private void EnsureLicensed()
    {
        if (!licenseValidator.ValidateSpaces())
        {
            SpacesLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Spaces feature.");
        }
    }

    // Success-path hand-off to the license validator's soft count check.
    // Failure isolation lives inside SpacesLicenseValidator.ValidateSpaceCountAsync; nothing here can throw.
    private async Task ValidateSpaceCountAsync(Ct ct) =>
        await licenseValidator.ValidateSpaceCountAsync(repository.GetActiveCountAsync, ct);

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

        var seen = new HashSet<(string? Origin, string? Path)>();
        foreach (var pattern in patterns)
        {
            if (pattern.Origin is null && string.IsNullOrEmpty(pattern.Path))
            {
                return StorageError.ValidationFailed(
                    "Each match pattern must have at least one of Origin or Path set.", "MatchPatterns");
            }

            if (!seen.Add(SpaceMatchPatternPolicy.NormalizePatternKey(pattern)))
            {
                return StorageError.ValidationFailed(
                    $"Duplicate match pattern: Origin='{pattern.Origin}', Path='{pattern.Path}'.", "MatchPatterns");
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

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Models;
using Duende.Spaces.Internal.Licensing;
using Duende.Spaces.Internal.Storage;

namespace Duende.Spaces.Internal;

/// <summary>
/// Internal implementation of <see cref="ISpaceAdmin"/> that delegates to <see cref="SpaceRepository"/>.
/// </summary>
internal sealed class SpaceAdmin(
    SpaceRepository repository,
    ISchemaStore? schemaStore,
    ISpaceContextAccessor spaceContextAccessor,
    SpacesLicenseValidator licenseValidator) : ISpaceAdmin
{
    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> CreateAsync(CreateSpaceConfiguration configuration, Ct ct)
    {
        EnsureLicensed();

        var nameError = ValidateName(configuration.Name);
        if (nameError is not null)
        {
            return SaveResult.Failure<SpaceId>(nameError);
        }

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

        return createResult.ToModel();
    }

    /// <inheritdoc/>
    public async Task<GetResult<SpaceConfiguration>> GetAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();
        return (await repository.GetByIdRawAsync(id, ct)).ToModel();
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> UpdateAsync(SpaceId id, SpaceConfiguration space,
        DataVersion expectedVersion, Ct ct)
    {
        EnsureLicensed();

        var nameError = ValidateName(space.Name);
        if (nameError is not null)
        {
            return SaveResult.Failure<SpaceId>(nameError);
        }

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

        if (!existing.Item.IsDeleted &&
            !SpaceMatchPatternPolicy.PatternsMatch(space.MatchPatterns, existing.Item.MatchPatterns))
        {
            var uniquenessError = await CheckPatternUniquenessAsync(space.MatchPatterns, excludeSpaceId: id, ct);
            if (uniquenessError is not null)
            {
                return SaveResult.Failure<SpaceId>(uniquenessError);
            }
        }

        return (await repository.UpdateAsync(space, extendedProperties, expectedVersion.Value, ct)).ToModel();
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();
        return (await repository.DeleteAsync(id, ct)).ToModel();
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> UndeleteAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();

        // Best-effort pre-check: the restored patterns must not use an origin both with and without
        // a path across spaces. The read here can go stale under a concurrent write; storage keys
        // stay the final word for pattern reservation, but origin/path consistency has no
        // authoritative re-check.
        var existing = await repository.GetByIdRawAsync(id, ct);
        if (!existing.Found)
        {
            return SaveResult.Failure<SpaceId>(StorageError.NotFound("space", id.Value.ToString()));
        }

        // An already-active space skips the check, so undelete stays a no-op success that still
        // clears its caches (see SpaceRepository.UndeleteAsync).
        if (existing.Item.IsDeleted)
        {
            var uniquenessError = await CheckPatternUniquenessAsync(existing.Item.MatchPatterns, excludeSpaceId: id, ct);
            if (uniquenessError is not null)
            {
                return SaveResult.Failure<SpaceId>(uniquenessError);
            }
        }

        var result = await repository.UndeleteAsync(id, ct);
        if (result.IsSuccess)
        {
            await ValidateSpaceCountAsync(ct);
        }

        return result.ToModel();
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> PurgeAsync(SpaceId id, Ct ct)
    {
        EnsureLicensed();
        return (await repository.PurgeAsync(id, ct)).ToModel();
    }

    /// <inheritdoc/>
    public async Task<QueryResult<SpaceListItem>> QueryAsync(QueryRequest<SpaceFilter, SpaceSortField> request, Ct ct)
    {
        EnsureLicensed();
        return (await StorageMapping.ExecuteQueryAsync(() => repository.QueryAsync(request.ToStorage(), ct))).ToModel(request.Range);
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
        Ct ct)
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

        IReadOnlyAttributeSchema? schema;
        using (spaceContextAccessor.SetSpace(SpaceId.Management))
        {
            schema = await schemaStore.GetAsync(SchemaId.Space, ct);
        }
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

            if (pattern.Origin is not null && !SpaceMatchPatternFormat.IsValidOrigin(pattern.Origin))
            {
                return StorageError.ValidationFailed(
                    $"Match pattern Origin '{pattern.Origin}' is invalid. {SpaceMatchPatternFormat.OriginRule}",
                    "MatchPatterns");
            }

            if (pattern.Path is not null && !SpaceMatchPatternFormat.IsValidPath(pattern.Path))
            {
                return StorageError.ValidationFailed(
                    $"Match pattern Path '{pattern.Path}' is invalid. {SpaceMatchPatternFormat.PathRule}",
                    "MatchPatterns");
            }

            if (!seen.Add(SpaceMatchPatternPolicy.NormalizePatternKey(pattern)))
            {
                return StorageError.ValidationFailed(
                    $"Duplicate match pattern: Origin='{pattern.Origin}', Path='{pattern.Path}'.", "MatchPatterns");
            }
        }

        return null;
    }

    private static StorageError? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return StorageError.ValidationFailed("Space name must not be empty or whitespace.", "Name");
        }

        return name.Length > 500
            ? StorageError.ValidationFailed("Space name must not exceed 500 characters.", "Name")
            : null;
    }

    private async Task<StorageError?> CheckPatternUniquenessAsync(
        IReadOnlyList<SpaceMatchPattern> patterns,
        SpaceId? excludeSpaceId,
        Ct ct)
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

            var originConsistencyError = await CheckOriginPathConsistencyAsync(pattern, excludeSpaceId, ct);
            if (originConsistencyError is not null)
            {
                return originConsistencyError;
            }
        }

        return null;
    }

    // One origin must not be registered both with a path and without a path across different spaces.
    // Deleted spaces count, because their patterns stay reserved until purge.
    // Path-only patterns (Origin is null) never query the origin index.
    // This is a best-effort check: concurrent writes on different spaces can both pass it.
    private async Task<StorageError?> CheckOriginPathConsistencyAsync(
        SpaceMatchPattern pattern,
        SpaceId? excludeSpaceId,
        Ct ct)
    {
        if (pattern.Origin is null)
        {
            return null;
        }

        bool conflict;
        if (pattern.Path is not null)
        {
            // (Origin, Path): conflicts if another space already registered the bare (Origin, null)
            // key, which is a direct, non-indexed key lookup.
            conflict = await repository.IsPatternRegisteredAsync(
                new SpaceMatchPattern { Origin = pattern.Origin, Path = null }, excludeSpaceId, ct);
        }
        else
        {
            // (Origin, null): conflicts if another space registered the same origin together with
            // any path. That space may use any path, so this needs the indexed lookup.
            conflict = await repository.IsOriginUsedWithPathByAnotherSpaceAsync(pattern.Origin, excludeSpaceId, ct);
        }

        return conflict
            ? StorageError.ValidationFailed(
                $"Match pattern origin '{pattern.Origin}' is already used with a different path configuration by another space.",
                "MatchPatterns")
            : null;
    }
}

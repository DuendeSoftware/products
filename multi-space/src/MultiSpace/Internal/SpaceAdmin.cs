// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.MultiSpace.Internal.Storage;
using Duende.Storage.Querying;

namespace Duende.MultiSpace.Internal;

/// <summary>
/// Internal implementation of <see cref="ISpaceAdmin"/> that delegates to <see cref="SpaceRepository"/>.
/// </summary>
internal sealed class SpaceAdmin(SpaceRepository repository) : ISpaceAdmin
{
    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> CreateAsync(CreateSpaceConfiguration configuration, Ct ct)
    {
        if (configuration.PoolId is { } poolId && poolId.Value <= 0)
        {
            return AdminError.ValidationFailed(
                "Pool ID must be a positive integer. Pool 0 is reserved for the default space.", "PoolId");
        }

        var validationError = ValidatePatterns(configuration.MatchPatterns);
        if (validationError is not null)
        {
            return validationError;
        }

        var uniquenessError = await CheckPatternUniquenessAsync(configuration.MatchPatterns, excludeSpaceId: null, ct);
        if (uniquenessError is not null)
        {
            return uniquenessError;
        }

        return await repository.CreateAsync(configuration.Name, configuration.MatchPatterns, configuration.PoolId, ct);
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
            return validationError;
        }

        // PoolId is immutable via UpdateAsync
        var existing = await repository.GetByIdAsync(id, ct);
        if (!existing.Found)
        {
            return AdminError.NotFound("space", id.Value.ToString());
        }

        if (space.PoolId.Value != existing.Item.PoolId.Value)
        {
            return AdminError.ValidationFailed(
                "Cannot change PoolId via Update. Use ChangePoolIdAsync instead.", "PoolId");
        }

        var uniquenessError = await CheckPatternUniquenessAsync(space.MatchPatterns, excludeSpaceId: id, ct);
        if (uniquenessError is not null)
        {
            return uniquenessError;
        }

        return await repository.UpdateAsync(space, expectedVersion.Value, ct);
    }

    /// <inheritdoc/>
    public async Task<SaveResult<SpaceId>> ChangePoolIdAsync(SpaceId id, PoolId newPoolId, Ct ct)
    {
        if (newPoolId.Value <= 0)
        {
            return AdminError.ValidationFailed(
                "Pool ID must be a positive integer. Pool 0 is reserved for the default space.", "PoolId");
        }

        return await repository.ChangePoolIdAsync(id, newPoolId.Value, ct);
    }

    /// <inheritdoc/>
    public Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, Ct ct) =>
        repository.DeleteAsync(id, ct);

    /// <inheritdoc/>
    public Task<QueryResult<SpaceListItem>> QueryAsync(QueryRequest<SpaceFilter, SpaceSortField> request, Ct ct) =>
        repository.QueryAsync(request, ct);

    private static AdminError? ValidatePatterns(IReadOnlyList<SpaceMatchPattern> patterns)
    {
        if (patterns.Count == 0)
        {
            return AdminError.ValidationFailed("At least one match pattern is required.", "MatchPatterns");
        }

        foreach (var pattern in patterns)
        {
            if (pattern.Origin is null && string.IsNullOrEmpty(pattern.Path))
            {
                return AdminError.ValidationFailed(
                    "Each match pattern must have at least one of Origin or Path set.", "MatchPatterns");
            }
        }

        return null;
    }

    private async Task<AdminError?> CheckPatternUniquenessAsync(
        IReadOnlyList<SpaceMatchPattern> patterns,
        SpaceId? excludeSpaceId,
        CancellationToken ct)
    {
        foreach (var pattern in patterns)
        {
            if (await repository.IsPatternRegisteredAsync(pattern, excludeSpaceId, ct))
            {
                return AdminError.AlreadyExists(
                    "match pattern",
                    $"Origin='{pattern.Origin}', Path='{pattern.Path}'",
                    "MatchPatterns");
            }
        }

        return null;
    }
}

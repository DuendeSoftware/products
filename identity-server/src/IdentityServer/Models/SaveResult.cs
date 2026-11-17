// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Represents the result of a save operation for an item identified by <typeparamref name="TId"/>.
/// </summary>
/// <typeparam name="TId">The type of the saved item's identifier.</typeparam>
public sealed record SaveResult<TId> where TId : notnull
{
    /// <summary>
    /// Gets a value indicating whether the save operation succeeded.
    /// When <see langword="true"/>, <see cref="Id"/> and <see cref="Version"/> are non-null.
    /// When <see langword="false"/>, <see cref="Errors"/> is non-null.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the identifier of the saved item, or <see langword="null"/> if the operation failed.
    /// </summary>
    public TId? Id { get; init; }

    /// <summary>
    /// Gets the version assigned after the save, or <see langword="null"/> if the operation failed.
    /// </summary>
    public DataVersion? Version { get; init; }

    /// <summary>
    /// Gets the list of errors that caused the failure, or <see langword="null"/> if the operation succeeded.
    /// </summary>
    public IReadOnlyList<StorageError>? Errors { get; init; }

    /// <inheritdoc/>
    public override string ToString() => IsSuccess
        ? $"Saved {typeof(TId).Name} with id {Id}, Version {Version}"
        : $"Failed to save {typeof(TId).Name}. Errors: {string.Join("", Errors?.Select(x => "\n - " + x) ?? [])}";
}

/// <summary>
/// Factory methods for creating <see cref="SaveResult{TId}"/> instances.
/// </summary>
public static class SaveResult
{
    /// <summary>Creates a successful save result.</summary>
    public static SaveResult<TId> Success<TId>(TId id, DataVersion version) where TId : notnull => new()
    {
        IsSuccess = true,
        Id = id,
        Version = version
    };

    /// <summary>Creates a failed save result.</summary>
    public static SaveResult<TId> Failure<TId>(params StorageError[] errors) where TId : notnull => new()
    {
        IsSuccess = false,
        Errors = errors
    };
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage;

/// <summary>
/// Represents the result of a save operation for an item identified by <typeparamref name="TId"/>.
/// </summary>
/// <typeparam name="TId">The type of the saved item's identifier.</typeparam>
public record SaveResult<TId> where TId : notnull
{
    internal SaveResult() { }

    /// <summary>
    /// Gets a value indicating whether the save operation succeeded.
    /// When <see langword="true"/>, <see cref="Id"/> and <see cref="Version"/> are non-null.
    /// When <see langword="false"/>, <see cref="Errors"/> is non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Id), nameof(Version))]
    [MemberNotNullWhen(false, nameof(Errors))]
    public bool IsSuccess { get; internal set; }

    /// <summary>
    /// Gets the identifier of the saved item, or <see langword="null"/> if the operation failed.
    /// </summary>
    public TId? Id { get; internal set; }

    /// <summary>
    /// Gets the version assigned after the save, or <see langword="null"/> if the operation failed.
    /// </summary>
    public DataVersion? Version { get; internal set; }

    /// <summary>
    /// Gets the list of errors that caused the failure, or <see langword="null"/> if the operation succeeded.
    /// </summary>
    public IReadOnlyList<StorageError>? Errors { get; internal set; }

    /// <inheritdoc/>
    public override string ToString() => IsSuccess
        ? $"Saved {typeof(TId).Name} with id {Id}, Version {Version}"
        : $"Failed to save {typeof(TId).Name}. Errors: {string.Join("", Errors?.Select(x => "\n - " + x) ?? [])}";
}

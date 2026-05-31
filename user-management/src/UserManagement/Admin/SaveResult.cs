// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Admin;

/// <summary>
/// Provides factory methods for creating <see cref="SaveResult{TId}"/> instances.
/// </summary>
public static class SaveResult
{
    /// <summary>
    /// Creates a <see cref="SaveResult{TId}"/> indicating a successful save operation.
    /// </summary>
    /// <typeparam name="TId">The type of the saved entity's identifier.</typeparam>
    /// <param name="id">The identifier of the saved entity.</param>
    /// <param name="version">The version assigned after the save.</param>
    public static SaveResult<TId> Success<TId>(TId id, DataVersion version) where TId : notnull =>
        new()
        {
            IsSuccess = true,
            Id = id,
            Version = version
        };

    /// <summary>
    /// Creates a <see cref="SaveResult{TId}"/> indicating a failed save operation.
    /// </summary>
    /// <typeparam name="TId">The type of the saved entity's identifier.</typeparam>
    /// <param name="errors">One or more errors that caused the failure.</param>
    public static SaveResult<TId> Failure<TId>(params AdminError[] errors) where TId : notnull =>
        new()
        {
            IsSuccess = false,
            Errors = errors
        };
}

/// <summary>
/// Represents the result of a save operation for an entity identified by <typeparamref name="TId"/>.
/// </summary>
/// <typeparam name="TId">The type of the saved entity's identifier.</typeparam>
public record SaveResult<TId> where TId : notnull
{
    /// <summary>
    /// Gets a value indicating whether the save operation succeeded.
    /// When <see langword="true"/>, <see cref="Id"/> and <see cref="Version"/> are non-null.
    /// When <see langword="false"/>, <see cref="Errors"/> is non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Id), nameof(Version))]
    [MemberNotNullWhen(false, nameof(Errors))]
    public bool IsSuccess { get; internal set; }

    /// <summary>
    /// Gets the identifier of the saved entity, or <see langword="null"/> if the operation failed.
    /// </summary>
    public TId? Id { get; internal set; }

    /// <summary>
    /// Gets the version assigned after the save, or <see langword="null"/> if the operation failed.
    /// </summary>
    public DataVersion? Version { get; internal set; }

    /// <summary>
    /// Gets the list of errors that caused the failure, or <see langword="null"/> if the operation succeeded.
    /// </summary>
    public IReadOnlyList<AdminError>? Errors { get; internal set; }

#pragma warning disable CA2225
    /// <summary>
    /// Implicitly converts a success tuple to a <see cref="SaveResult{TId}"/>.
    /// </summary>
    /// <param name="success">A tuple containing the entity identifier and version.</param>
    public static implicit operator SaveResult<TId>((TId Id, DataVersion version) success) => new()
    {
        IsSuccess = true,
        Id = success.Id,
        Version = success.version
    };

    /// <summary>
    /// Implicitly converts an array of <see cref="AdminError"/> to a failed <see cref="SaveResult{TId}"/>.
    /// </summary>
    /// <param name="errors">The errors that caused the failure.</param>
    public static implicit operator SaveResult<TId>(AdminError[] errors) => new()
    {
        IsSuccess = false,
        Errors = errors
    };

    /// <summary>
    /// Implicitly converts a single <see cref="AdminError"/> to a failed <see cref="SaveResult{TId}"/>.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    public static implicit operator SaveResult<TId>(AdminError error) => new()
    {
        IsSuccess = false,
        Errors = [error]
    };

    /// <inheritdoc/>
    public override string ToString() => IsSuccess
        ? $"Saved {typeof(TId).Name} with id {Id}, Version {Version}"
        : $"Failed to save {typeof(TId).Name}. Errors: {string.Join(' ', Errors.Select(x => "\n - " + x))}";
#pragma warning restore CA2225
}

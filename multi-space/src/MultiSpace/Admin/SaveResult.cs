// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.MultiSpace;

/// <summary>
/// Provides factory methods for creating <see cref="SaveResult{TId}"/> instances.
/// </summary>
public static class SaveResult
{
    /// <summary>Creates a successful save result.</summary>
    public static SaveResult<TId> Success<TId>(TId id, DataVersion version) where TId : notnull =>
        new() { IsSuccess = true, Id = id, Version = version };

    /// <summary>Creates a failed save result.</summary>
    public static SaveResult<TId> Failure<TId>(params AdminError[] errors) where TId : notnull =>
        new() { IsSuccess = false, Errors = errors };
}

/// <summary>
/// Represents the result of a save operation for an entity identified by <typeparamref name="TId"/>.
/// </summary>
/// <typeparam name="TId">The type of the saved entity's identifier.</typeparam>
public record SaveResult<TId> where TId : notnull
{
    /// <summary>Gets a value indicating whether the save operation succeeded.</summary>
    [MemberNotNullWhen(true, nameof(Id), nameof(Version))]
    [MemberNotNullWhen(false, nameof(Errors))]
    public bool IsSuccess { get; internal set; }

    /// <summary>Gets the identifier of the saved entity, or null if the operation failed.</summary>
    public TId? Id { get; internal set; }

    /// <summary>Gets the version assigned after the save, or null if the operation failed.</summary>
    public DataVersion? Version { get; internal set; }

    /// <summary>Gets the errors that caused the failure, or null if the operation succeeded.</summary>
    public IReadOnlyList<AdminError>? Errors { get; internal set; }

#pragma warning disable CA2225
    /// <summary>Implicitly converts a success tuple to a SaveResult.</summary>
    public static implicit operator SaveResult<TId>((TId Id, DataVersion version) success) => new()
    {
        IsSuccess = true,
        Id = success.Id,
        Version = success.version
    };

    /// <summary>Implicitly converts an array of AdminError to a failed SaveResult.</summary>
    public static implicit operator SaveResult<TId>(AdminError[] errors) => new()
    {
        IsSuccess = false,
        Errors = errors
    };

    /// <summary>Implicitly converts a single AdminError to a failed SaveResult.</summary>
    public static implicit operator SaveResult<TId>(AdminError error) => new()
    {
        IsSuccess = false,
        Errors = [error]
    };
#pragma warning restore CA2225
}

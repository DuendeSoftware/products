// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Provides factory methods for creating <see cref="SaveResult{TId}"/> instances.
/// </summary>
public static class SaveResult
{
    /// <summary>
    /// Creates a <see cref="SaveResult{TId}"/> indicating a successful save operation.
    /// </summary>
    /// <typeparam name="TId">The type of the saved item's identifier.</typeparam>
    /// <param name="id">The identifier of the saved item.</param>
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
    /// <typeparam name="TId">The type of the saved item's identifier.</typeparam>
    /// <param name="errors">One or more errors that caused the failure.</param>
    public static SaveResult<TId> Failure<TId>(params StorageError[] errors) where TId : notnull =>
        new()
        {
            IsSuccess = false,
            Errors = errors ?? throw new ArgumentNullException(nameof(errors))
        };
}

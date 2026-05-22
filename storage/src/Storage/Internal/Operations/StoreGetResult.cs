// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Wraps the result of a Get operation from the store.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record StoreGetResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreGetResult"/> class representing a found entity.
    /// </summary>
    /// <param name="dso">The data storage object.</param>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <param name="version">The version of the entity.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="lastUpdatedAt">The last update timestamp.</param>
    public StoreGetResult(IDataStorageObject dso, Guid id, int version, DateTimeOffset createdAt, DateTimeOffset lastUpdatedAt)
    {
        Found = true;
        Dso = dso;
        Id = id;
        Version = version;
        CreatedAt = createdAt;
        LastUpdatedAt = lastUpdatedAt;
    }

    private StoreGetResult()
    {
    }

    /// <summary>
    /// Creates a result indicating the entity was not found.
    /// </summary>
    /// <returns>A <see cref="StoreGetResult"/> representing a not-found result.</returns>
    public static StoreGetResult NotFound() => new();

    /// <summary>
    /// Gets a value indicating whether the entity was found.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Dso))]
    [MemberNotNullWhen(true, nameof(Id))]
    [MemberNotNullWhen(true, nameof(Version))]
    public bool Found { get; }

    /// <summary>
    /// Gets the data storage object, or <c>null</c> if not found.
    /// </summary>
    public IDataStorageObject? Dso { get; }

    /// <summary>
    /// Gets the unique identifier, or <c>null</c> if not found.
    /// </summary>
    public Guid? Id { get; }

    /// <summary>
    /// Gets the entity version, or <c>null</c> if not found.
    /// </summary>
    public int? Version { get; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the last update timestamp.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; }

    /// <summary>
    /// Creates a result indicating the entity was found.
    /// </summary>
    /// <param name="item">The data storage object.</param>
    /// <param name="id">The unique identifier.</param>
    /// <param name="version">The entity version.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="lastUpdatedAt">The last update timestamp.</param>
    /// <returns>A <see cref="StoreGetResult"/> representing a found result.</returns>
    public static StoreGetResult IsFound(IDataStorageObject item, Guid id, int version, DateTimeOffset createdAt, DateTimeOffset lastUpdatedAt) =>
        new(item, id, version, createdAt, lastUpdatedAt);
}

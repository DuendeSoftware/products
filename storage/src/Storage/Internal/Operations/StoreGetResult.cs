// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Wraps the result of a Get operation from the store.
/// </summary>
public sealed record StoreGetResult
{
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

    public static StoreGetResult NotFound() => new();

    [MemberNotNullWhen(true, nameof(Dso))]
    [MemberNotNullWhen(true, nameof(Id))]
    [MemberNotNullWhen(true, nameof(Version))]
    public bool Found { get; }

    public IDataStorageObject? Dso { get; }

    public Guid? Id { get; }

    public int? Version { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastUpdatedAt { get; }

    public static StoreGetResult IsFound(IDataStorageObject item, Guid id, int version, DateTimeOffset createdAt, DateTimeOffset lastUpdatedAt) =>
        new(item, id, version, createdAt, lastUpdatedAt);
}

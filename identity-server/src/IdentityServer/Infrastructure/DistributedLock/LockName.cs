// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Spaces;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// Identifies a distributed lock by a stable, well-known name. Names are either produced by one of the
/// factories below, or loaded back from storage (<c>Load</c>, generated). <see cref="Value"/> strings
/// are permanent once shipped: changing one would stop a rolling upgrade from recognizing an in-flight lock.
/// </summary>
[StringValue]
internal partial record LockName
{
    private const int MaxLength = 100;

    private static bool TryValidate(string? input, out IReadOnlyList<string>? errors)
    {
        errors = null;

        if (input is null or { Length: 0 })
        {
            errors = ["A value is required."];
            return false;
        }

        if (input.Length > MaxLength)
        {
            errors = [$"Must not exceed {MaxLength} characters."];
            return false;
        }

        foreach (var c in input.AsSpan())
        {
            if (!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '/'))
            {
                errors = ["Must only contain lowercase ASCII letters, digits, '-' or '/'."];
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the lock name used while creating or rotating signing keys for a Space.</summary>
    public static LockName SigningKeys(SpaceId spaceId) => new($"signing-keys/{spaceId.Value:D}");

    /// <summary>Gets the lock name used to coordinate the outbox processor background job.</summary>
    public static LockName OutboxProcessor { get; } = new("jobs/outbox-processor");

    /// <summary>Gets the lock name used to coordinate the storage purge background job.</summary>
    public static LockName StoragePurge { get; } = new("jobs/storage-purge");

    /// <summary>Gets the lock name used to coordinate the server-side session cleanup background job.</summary>
    public static LockName ServerSideSessionCleanup { get; } = new("jobs/server-side-session-cleanup");
}

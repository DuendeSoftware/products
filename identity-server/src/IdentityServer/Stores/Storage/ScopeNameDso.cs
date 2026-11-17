// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// A guard entity that reserves an API scope or identity resource name, so the name stays unique across
/// both kinds (Duende.IdentityServer validates token requests against the union of both stores, and a
/// clash makes that validation throw). Its own ID is the same as the ID of the API scope or identity
/// resource it guards, and its unique key (<see cref="ScopeNameGuardDskV1"/>) is the name itself: creating
/// a guard for a name already guarded by the other kind fails with <c>KeyConflict</c>.
/// </summary>
/// <remarks>
/// Rows created before this guard existed have no corresponding <see cref="ScopeNameDso"/>. Renaming or
/// deleting such a row tolerates the missing guard: Storage's delete is idempotent for a missing key, so
/// no special-casing is needed in the repositories.
/// </remarks>
internal static class ScopeNameDso
{
    internal static readonly EntityType EntityType = new(2114, "ScopeNameDso");

    /// <summary>
    /// Converts a stored ScopeNameDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(ScopeNameDso)} type {dso.GetType()}.")
    };

    internal sealed record V1 : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);

        public required string Name { get; init; }
    }
}

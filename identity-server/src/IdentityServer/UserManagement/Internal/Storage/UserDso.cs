// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.UserManagement.Internal.Storage;

internal static class UserDso
{
    internal static readonly EntityType EntityType = new(900, "UserDso");

    /// <summary>
    /// Converts a stored UserDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(UserDso)} type {dso.GetType()}.")
    };

    internal sealed record V1(
        Guid Id,
        string SubjectId,
        IReadOnlyList<AspectRef> Aspects) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }

    internal sealed record AspectRef(Guid Id, int Version, uint AspectEntityTypeId);
}

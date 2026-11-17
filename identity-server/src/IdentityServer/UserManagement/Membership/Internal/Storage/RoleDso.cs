// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.UserManagement.Membership.Internal.Storage;

internal static class RoleDso
{
    internal static readonly EntityType EntityType = new(1502, "RoleDso");

    /// <summary>
    /// Converts a stored RoleDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(RoleDso)} type {dso.GetType()}.")
    };

    internal sealed record V1(Guid Id, string RoleId, string Name, string? Description) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}

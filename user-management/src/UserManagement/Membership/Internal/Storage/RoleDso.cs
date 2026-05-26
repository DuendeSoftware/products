// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Membership.Internal.Storage;

internal static class RoleDso
{
    internal static readonly EntityType EntityType = new(1502, "RoleDso");

    internal sealed record V1(Guid Id, string RoleId, string Name, string? Description) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}

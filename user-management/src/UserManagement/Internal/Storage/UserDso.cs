// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Internal.Storage;

internal static class UserDso
{
    internal static readonly EntityType EntityType = new(900, "UserDso");

    internal sealed record V1(
        Guid Id,
        string SubjectId,
        IReadOnlyList<AspectRef> Aspects) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }

    internal sealed record AspectRef(Guid Id, int Version, uint AspectEntityTypeId);
}

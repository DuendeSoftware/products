// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;

namespace Duende.UserManagement.Profiles.Internal.Storage;

internal static class UserProfileDso
{
    internal static readonly EntityType EntityType = new(1500, "UserProfileDso");

    internal sealed record V1(Guid Id, string SubjectId, List<AttributeValueDso.V1> Attributes) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}

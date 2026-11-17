// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions.Internal.Storage;
using Duende.Storage.Internal;

namespace Duende.UserManagement.Profiles.Internal.Storage;

internal static class UserProfileDso
{
    internal static readonly EntityType EntityType = new(1500, "UserProfileDso");

    /// <summary>
    /// Converts a stored UserProfileDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(UserProfileDso)} type {dso.GetType()}.")
    };

    internal sealed record V1(Guid Id, string SubjectId, IReadOnlyList<AttributeValueDso.V1> Attributes) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

/// <summary>
///     Provides well-known <see cref="SchemaId"/> constants for User Management schemas.
/// </summary>
public static class SchemaIdExtensions
{
    private static readonly SchemaId UserProfileSchemaId = SchemaId.Create("user-profile");

    extension(SchemaId)
    {
        /// <summary>The well-known schema ID for user profile attributes.</summary>
        public static SchemaId UserProfile => UserProfileSchemaId;
    }
}

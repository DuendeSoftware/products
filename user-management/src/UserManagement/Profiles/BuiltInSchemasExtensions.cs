// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

/// <summary>
/// User Management's built-in schemas.
/// </summary>
public static class BuiltInSchemasExtensions
{
    extension(BuiltInSchemas)
    {
        /// <summary>
        /// The default OIDC-shaped user profile schema (email, name, given_name and family_name) that
        /// <c>AddUserManagement</c> registers. Each access returns a new instance.
        /// </summary>
        /// <remarks>
        /// Extend it with <c>BuiltInSchemas.UserProfile.Extend(...)</c>, or replace it with a full schema
        /// for <c>SchemaId.UserProfile</c>, and register the result through
        /// <c>AddInMemoryDataExtensionSchemas</c>. Either order relative to <c>AddUserManagement</c> works.
        /// </remarks>
        public static SchemaConfiguration UserProfile => new()
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "User Profile",
            AttributeDefinitions =
            [
                OidcStandardAttributes.Email with { IsUnique = true, IsRequired = true },
                OidcStandardAttributes.Name,
                OidcStandardAttributes.GivenName,
                OidcStandardAttributes.FamilyName
            ]
        };
    }
}

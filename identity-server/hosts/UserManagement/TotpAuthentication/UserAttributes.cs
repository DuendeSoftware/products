// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement.Profiles;

namespace TotpAuthentication;

internal static class UserAttributes
{
    internal static readonly AttributeDefinition AuthenticatorKey = new()
    {
        Code = AttributeCode.Create("authenticator_key"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("TOTP authenticator key (base32 encoded).")
    };

    internal static readonly AttributeDefinition Email = new()
    {
        Code = AttributeCode.Create("email"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Email"),
        IsUnique = true
    };

    internal static readonly AttributeDefinition UserName = new()
    {
        Code = AttributeCode.Create("userName"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Unique identifier for the user."),
        IsUnique = true
    };

    internal static async Task BootstrapAsync(this ISchemaAdmin schemaAdmin, Ct ct)
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.AttributeDefinitions.Add(AuthenticatorKey);
        schema.AttributeDefinitions.Add(Email);
        schema.AttributeDefinitions.Add(UserName);

        if (getResult.Found)
        {
            await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, ct);
        }
        else
        {
            await schemaAdmin.CreateAsync(schema, ct);
        }
    }
}

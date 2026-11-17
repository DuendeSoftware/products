// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement.Profiles;

namespace AuthenticationFlowWithWebComponent;

internal static class UserAttributes
{
    internal static readonly AttributeDefinition AuthenticatorKey = new()
    {
        Code = AttributeCode.Create("authenticator_key"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("TOTP authenticator key (base32 encoded).")
    };

    internal static readonly AttributeDefinition Name = new()
    {
        Code = AttributeCode.Create("name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("User's name.")
    };

    internal static async Task BootstrapAsync(this ISchemaAdmin schemaAdmin, Ct ct)
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.AttributeDefinitions.Add(AuthenticatorKey);
        schema.AttributeDefinitions.Add(Name);

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

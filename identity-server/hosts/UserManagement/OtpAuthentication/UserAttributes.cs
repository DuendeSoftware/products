// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement.Profiles;

namespace OtpAuthentication;

internal static class UserAttributes
{
    internal static readonly AttributeDefinition Name = new()
    {
        Code = AttributeCode.Create("name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Your full name in displayable form including all name parts, titles and suffixes, ordered according to your locale and preferences.")
    };

    internal static async Task BootstrapAsync(this ISchemaAdmin schemaAdmin, Ct ct)
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.AttributeDefinitions.Add(Name);

        if (getResult.Found)
        {
            _ = await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, ct);
        }
        else
        {
            _ = await schemaAdmin.CreateAsync(schema, ct);
        }
    }
}

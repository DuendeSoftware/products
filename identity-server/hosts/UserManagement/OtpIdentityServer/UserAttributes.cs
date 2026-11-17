// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;

namespace OtpIdentityServer;

public static class UserAttributes
{
    public static readonly AttributeDefinition Email = new()
    {
        Code = AttributeCode.Create("email"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("User's email address.")
    };

    public static readonly AttributeDefinition GivenName = new()
    {
        Code = AttributeCode.Create("given_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("User's given name (first name).")
    };

    public static readonly AttributeDefinition FamilyName = new()
    {
        Code = AttributeCode.Create("family_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("User's family name (last name).")
    };

    public static readonly AttributeDefinition Picture = new()
    {
        Code = AttributeCode.Create("picture"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("URL of the user's profile picture.")
    };
}

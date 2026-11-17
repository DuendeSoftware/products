// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement.Profiles;

namespace PlatformUserManagementApp;

/// <summary>
/// Defines and seeds the SCIM User attribute schema for the Platform user store.
/// Attributes follow SCIM 2.0 User schema (https://datatracker.ietf.org/doc/html/rfc7643).
/// Attribute names use underscores since dots are not allowed in AttributeCode.
/// </summary>
internal static class ScimSchema
{
    // --- Groups ---
    private static readonly AttributeGroupCode CoreGroup = AttributeGroupCode.Create("core");
    private static readonly AttributeGroupCode NameGroup = AttributeGroupCode.Create("name");
    private static readonly AttributeGroupCode EmailGroup = AttributeGroupCode.Create("emails");
    private static readonly AttributeGroupCode PhoneGroup = AttributeGroupCode.Create("phoneNumbers");
    private static readonly AttributeGroupCode AddressGroup = AttributeGroupCode.Create("addresses");

    internal static IReadOnlyList<AttributeGroup> AllGroups =>
    [
        new(CoreGroup, AttributeDisplayName.Create("Core"), AttributeDescription.Create("Core user attributes."), 0),
        new(NameGroup, AttributeDisplayName.Create("Name"), AttributeDescription.Create("Components of the user's real name."), 1),
        new(EmailGroup, AttributeDisplayName.Create("Email"), AttributeDescription.Create("Email addresses."), 2),
        new(PhoneGroup, AttributeDisplayName.Create("Phone Numbers"), AttributeDescription.Create("Phone numbers."), 3),
        new(AddressGroup, AttributeDisplayName.Create("Addresses"), AttributeDescription.Create("Physical mailing addresses."), 4),
    ];

    // Core scalar attributes
    internal static readonly AttributeDefinition UserName = Attr("userName", "Unique identifier for the user.", CoreGroup, 0);
    internal static readonly AttributeDefinition DisplayName = Attr("displayName", "Name of the user suitable for display.", CoreGroup, 1);
    internal static readonly AttributeDefinition Title = Attr("title", "The user's title, such as 'Vice President'.", CoreGroup, 2);
    internal static readonly AttributeDefinition UserType = Attr("userType", "Used to identify the relationship between the organization and the user.", CoreGroup, 3);
    internal static readonly AttributeDefinition PreferredLanguage = Attr("preferredLanguage", "Indicates the user's preferred written or spoken language.", CoreGroup, 4);
    internal static readonly AttributeDefinition Locale = Attr("locale", "Used to indicate the User's default location for purposes of localizing items.", CoreGroup, 5);
    internal static readonly AttributeDefinition Timezone = Attr("timezone", "The User's time zone in the 'Olson' time zone database format.", CoreGroup, 6);
    internal static readonly AttributeDefinition Active = Attr("active", "A Boolean value indicating the User's administrative status.", ScalarDataType.Boolean, CoreGroup, 7);

    internal static readonly AttributeDefinition Name = new()
    {
        Code = AttributeCode.Create("name"),
        AttributeType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>()
        {
            [AttributeCode.Create("formatted")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("familyName")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("givenName")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("middleName")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("honorificPrefix")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("honorificSuffix")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        }),
        Description = AttributeDescription.Create("The components of the user's real name."),
        GroupCode = NameGroup,
        Order = 0
    };

    internal static readonly ComplexAttributeType Email = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
    {
        [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
    });

    internal static readonly AttributeDefinition Emails = new AttributeDefinition
    {
        Code = AttributeCode.Create("emails"),
        AttributeType = new ListAttributeType(Email),
        Description = AttributeDescription.Create("Email addresses."),
        GroupCode = EmailGroup,
        Order = 0
    };

    internal static readonly ComplexAttributeType PhoneNumber = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
    {
        [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
    });

    internal static readonly AttributeDefinition PhoneNumbers = new AttributeDefinition
    {
        Code = AttributeCode.Create("phoneNumbers"),
        AttributeType = new ListAttributeType(PhoneNumber),
        Description = AttributeDescription.Create("Phone numbers."),
        GroupCode = PhoneGroup,
        Order = 0
    };

    // addresses[] multi-valued complex
    internal static readonly ComplexAttributeType Address = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
    {
        [AttributeCode.Create("formatted")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("streetAddress")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("locality")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("region")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("postalCode")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("country")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
    });

    internal static readonly AttributeDefinition Addresses = new AttributeDefinition
    {
        Code = AttributeCode.Create("addresses"),
        AttributeType = new ListAttributeType(Address),
        Description = AttributeDescription.Create("Physical mailing addresses."),
        GroupCode = AddressGroup,
        Order = 0
    };

    internal static IReadOnlyList<AttributeDefinition> All =>
    [
        DisplayName, Title, UserType, PreferredLanguage, Locale, Timezone, Active,
        Name,
        Emails,
        PhoneNumbers,
        Addresses
    ];

    internal static async Task SeedAsync(ISchemaAdmin schemaAdmin, Ct ct)
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        // Seed groups
        foreach (var group in AllGroups)
        {
            if (!schema.Groups.Any(g => g.Code == group.Code))
            {
                schema.Groups.Add(group);
            }
        }

        // Seed attribute definitions
        foreach (var definition in All)
        {
            if (!schema.AttributeDefinitions.Any(d => d.Code == definition.Code))
            {
                schema.AttributeDefinitions.Add(definition);
            }
        }

        if (getResult.Found)
        {
            _ = await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, ct);
        }
        else
        {
            _ = await schemaAdmin.CreateAsync(schema, ct);
        }
    }

    private static AttributeDefinition Attr(string name, string description, AttributeGroupCode groupName, int order) =>
        new() { Code = AttributeCode.Create(name), AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create(description), GroupCode = groupName, Order = order };

    private static AttributeDefinition Attr(string name, string description, ScalarDataType type, AttributeGroupCode groupName, int order) =>
        new() { Code = AttributeCode.Create(name), AttributeType = new ScalarAttributeType(type), Description = AttributeDescription.Create(description), GroupCode = groupName, Order = order };
}

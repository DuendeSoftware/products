// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Scim;

/// <summary>
/// Provides an opt-in profile schema for the SCIM core User resource defined by RFC 7643.
/// </summary>
public static class DefaultScimUserSchema
{
    private static readonly AttributeGroupCode CoreGroup = AttributeGroupCode.Create("scim_core");
    private static readonly AttributeGroupCode NameGroup = AttributeGroupCode.Create("scim_name");
    private static readonly AttributeGroupCode ContactGroup = AttributeGroupCode.Create("scim_contact");
    private static readonly AttributeGroupCode AddressGroup = AttributeGroupCode.Create("scim_addresses");
    private static readonly AttributeGroupCode OrganizationGroup = AttributeGroupCode.Create("scim_organization");
    private static readonly AttributeGroupCode SecurityGroup = AttributeGroupCode.Create("scim_security");

    private static readonly IReadOnlyList<AttributeGroup> DefaultGroups = Array.AsReadOnly<AttributeGroup>(
    [
        new(CoreGroup, AttributeDisplayName.Create("SCIM Core"), AttributeDescription.Create("Core SCIM User attributes."), 0),
        new(NameGroup, AttributeDisplayName.Create("SCIM Name"), AttributeDescription.Create("Components of the user's name."), 1),
        new(ContactGroup, AttributeDisplayName.Create("SCIM Contact"), AttributeDescription.Create("Contact details for the user."), 2),
        new(AddressGroup, AttributeDisplayName.Create("SCIM Addresses"), AttributeDescription.Create("Physical mailing addresses for the user."), 3),
        new(OrganizationGroup, AttributeDisplayName.Create("SCIM Organization"), AttributeDescription.Create("Organizational relationships and assignments."), 4),
        new(SecurityGroup, AttributeDisplayName.Create("SCIM Security"), AttributeDescription.Create("Security-related SCIM User attributes."), 5)
    ]);

    /// <summary>
    /// Gets the attribute groups for the default SCIM User schema.
    /// </summary>
    public static IReadOnlyList<AttributeGroup> Groups => DefaultGroups;

    /// <summary>Unique identifier for the User, typically used by the user to directly authenticate.</summary>
    public static AttributeDefinition UserName { get; } =
        Scalar("userName", "Unique identifier for the User, typically used by the user to directly authenticate.", CoreGroup, 0, isQueryable: true, isRequired: true, isUnique: true);

    /// <summary>The components of the user's real name.</summary>
    public static AttributeDefinition Name { get; } =
        Complex("name", "The components of the user's real name.", NameType(), NameGroup, 0);

    /// <summary>The name of the User, suitable for display to end-users.</summary>
    public static AttributeDefinition DisplayName { get; } =
        Scalar("displayName", "The name of the User, suitable for display to end-users.", CoreGroup, 1, isQueryable: true);

    /// <summary>The casual way to address the User in real life.</summary>
    public static AttributeDefinition NickName { get; } =
        Scalar("nickName", "The casual way to address the User in real life.", CoreGroup, 2);

    /// <summary>A URI that is a uniform resource locator and points to a location representing the User's online profile.</summary>
    public static AttributeDefinition ProfileUrl { get; } =
        Scalar("profileUrl", "A URI that is a uniform resource locator and points to a location representing the User's online profile.", CoreGroup, 3);

    /// <summary>The User's title, such as "Vice President".</summary>
    public static AttributeDefinition Title { get; } =
        Scalar("title", "The User's title, such as \"Vice President\".", OrganizationGroup, 0, isQueryable: true);

    /// <summary>Identifies the relationship between the organization and the User.</summary>
    public static AttributeDefinition UserType { get; } =
        Scalar("userType", "Identifies the relationship between the organization and the User.", OrganizationGroup, 1, isQueryable: true);

    /// <summary>The User's preferred written or spoken language.</summary>
    public static AttributeDefinition PreferredLanguage { get; } =
        Scalar("preferredLanguage", "The User's preferred written or spoken language.", CoreGroup, 4);

    /// <summary>The User's default location for localizing items such as currency, date time format, or numerical representations.</summary>
    public static AttributeDefinition Locale { get; } =
        Scalar("locale", "The User's default location for localizing items such as currency, date time format, or numerical representations.", CoreGroup, 5);

    /// <summary>The User's time zone in the IANA time zone database format.</summary>
    public static AttributeDefinition Timezone { get; } =
        Scalar("timezone", "The User's time zone in the IANA time zone database format.", CoreGroup, 6);

    /// <summary>Indicates the User's administrative status.</summary>
    public static AttributeDefinition Active { get; } =
        Scalar("active", "Indicates the User's administrative status.", CoreGroup, 7, ScalarDataType.Boolean, isQueryable: true);

    /// <summary>Email addresses for the User.</summary>
    public static AttributeDefinition Emails { get; } =
        ComplexList("emails", "Email addresses for the User.", TypedValueType(), ContactGroup, 0);

    /// <summary>Phone numbers for the User.</summary>
    public static AttributeDefinition PhoneNumbers { get; } =
        ComplexList("phoneNumbers", "Phone numbers for the User.", TypedValueType(), ContactGroup, 1);

    /// <summary>Instant messaging addresses for the User.</summary>
    public static AttributeDefinition Ims { get; } =
        ComplexList("ims", "Instant messaging addresses for the User.", TypedValueType(), ContactGroup, 2);

    /// <summary>URIs of photos of the User.</summary>
    public static AttributeDefinition Photos { get; } =
        ComplexList("photos", "URIs of photos of the User.", TypedValueType(), ContactGroup, 3);

    /// <summary>Physical mailing addresses for the User.</summary>
    public static AttributeDefinition Addresses { get; } =
        ComplexList("addresses", "Physical mailing addresses for the User.", AddressType(), AddressGroup, 0);

    /// <summary>Entitlements for the User.</summary>
    public static AttributeDefinition Entitlements { get; } =
        ComplexList("entitlements", "Entitlements for the User.", TypedValueType(), OrganizationGroup, 3);

    /// <summary>Roles for the User.</summary>
    public static AttributeDefinition Roles { get; } =
        ComplexList("roles", "Roles for the User.", TypedValueType(), OrganizationGroup, 4);

    /// <summary>X.509 certificates associated with the User.</summary>
    public static AttributeDefinition X509Certificates { get; } =
        ComplexList("x509Certificates", "X.509 certificates associated with the User.", CertificateType(), SecurityGroup, 1);

    /// <summary>
    /// Gets the attribute definitions for the default SCIM User schema.
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> AttributeDefinitions { get; } = Array.AsReadOnly<AttributeDefinition>(
    [
        UserName,
        Name,
        DisplayName,
        NickName,
        ProfileUrl,
        Title,
        UserType,
        PreferredLanguage,
        Locale,
        Timezone,
        Active,
        Emails,
        PhoneNumbers,
        Ims,
        Photos,
        Addresses,
        Entitlements,
        Roles,
        X509Certificates
    ]);

    private static AttributeDefinition Scalar(
        string code,
        string description,
        AttributeGroupCode groupCode,
        int order) =>
        Scalar(code, description, groupCode, order, ScalarDataType.String, false, false, false);

    private static AttributeDefinition Scalar(
        string code,
        string description,
        AttributeGroupCode groupCode,
        int order,
        bool isQueryable) =>
        Scalar(code, description, groupCode, order, ScalarDataType.String, isQueryable, false, false);

    private static AttributeDefinition Scalar(
        string code,
        string description,
        AttributeGroupCode groupCode,
        int order,
        bool isQueryable,
        bool isRequired,
        bool isUnique) =>
        Scalar(code, description, groupCode, order, ScalarDataType.String, isQueryable, isRequired, isUnique);

    private static AttributeDefinition Scalar(
        string code,
        string description,
        AttributeGroupCode groupCode,
        int order,
        ScalarDataType dataType,
        bool isQueryable) =>
        Scalar(code, description, groupCode, order, dataType, isQueryable, false, false);

    private static AttributeDefinition Scalar(
        string code,
        string description,
        AttributeGroupCode groupCode,
        int order,
        ScalarDataType dataType,
        bool isQueryable,
        bool isRequired,
        bool isUnique) =>
        new()
        {
            Code = AttributeCode.Create(code),
            AttributeType = new ScalarAttributeType(dataType),
            Description = AttributeDescription.Create(description),
            IsQueryable = isQueryable,
            IsRequired = isRequired,
            IsUnique = isUnique,
            GroupCode = groupCode,
            Order = order
        };

    private static AttributeDefinition Complex(
        string code,
        string description,
        ComplexAttributeType attributeType,
        AttributeGroupCode groupCode,
        int order) =>
        new()
        {
            Code = AttributeCode.Create(code),
            AttributeType = attributeType,
            Description = AttributeDescription.Create(description),
            IsQueryable = false,
            GroupCode = groupCode,
            Order = order
        };

    private static AttributeDefinition ComplexList(
        string code,
        string description,
        ComplexAttributeType elementType,
        AttributeGroupCode groupCode,
        int order) =>
        new()
        {
            Code = AttributeCode.Create(code),
            AttributeType = new ListAttributeType(elementType),
            Description = AttributeDescription.Create(description),
            IsQueryable = false,
            GroupCode = groupCode,
            Order = order
        };

    private static ComplexAttributeType NameType() =>
        ComplexType(
            ("formatted", "The full name, including all middle names, titles, and suffixes."),
            ("familyName", "The family name of the User."),
            ("givenName", "The given name of the User."),
            ("middleName", "The middle name of the User."),
            ("honorificPrefix", "The honorific prefix or title of the User."),
            ("honorificSuffix", "The honorific suffix of the User."));

    private static ComplexAttributeType TypedValueType() =>
        ComplexType(
            ("value", "The attribute's value.", ScalarDataType.String),
            ("display", "A human-readable name for the value.", ScalarDataType.String),
            ("type", "A label indicating the attribute's function.", ScalarDataType.String),
            ("primary", "Indicates whether this is the primary value.", ScalarDataType.Boolean));

    private static ComplexAttributeType AddressType() =>
        ComplexType(
            ("formatted", "The full mailing address, formatted for display or use with a mailing label.", ScalarDataType.String),
            ("streetAddress", "The full street address component.", ScalarDataType.String),
            ("locality", "The city or locality component.", ScalarDataType.String),
            ("region", "The state or region component.", ScalarDataType.String),
            ("postalCode", "The zip code or postal code component.", ScalarDataType.String),
            ("country", "The country component.", ScalarDataType.String),
            ("type", "A label indicating the attribute's function.", ScalarDataType.String),
            ("primary", "Indicates whether this is the primary value.", ScalarDataType.Boolean));

    private static ComplexAttributeType CertificateType() =>
        ComplexType(
            ("value", "The value of an X.509 certificate.", ScalarDataType.String),
            ("display", "A human-readable name for the certificate.", ScalarDataType.String),
            ("type", "A label indicating the certificate's function.", ScalarDataType.String),
            ("primary", "Indicates whether this is the primary value.", ScalarDataType.Boolean));

    private static ComplexAttributeType ComplexType(params (string Code, string Description)[] properties) =>
        ComplexType([.. properties.Select(p => (p.Code, p.Description, ScalarDataType.String))]);

    private static ComplexAttributeType ComplexType(params (string Code, string Description, ScalarDataType DataType)[] properties)
    {
        var result = properties.ToDictionary(
            property => AttributeCode.Create(property.Code),
            property => ComplexAttributeProperty.Of(
                new ScalarAttributeType(property.DataType),
                null,
                AttributeDescription.Create(property.Description)));

        return new ComplexAttributeType(new ReadOnlyDictionary<AttributeCode, ComplexAttributeProperty>(result));
    }
}

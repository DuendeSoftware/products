// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

/// <summary>
/// Provides pre-built <see cref="AttributeDefinition"/> instances for the standard OIDC claims
/// defined in the OpenID Connect Core 1.0 specification.
/// </summary>
public static class OidcStandardAttributes
{
    /// <summary>
    /// Attribute definition for the <c>name</c> OIDC claim.
    /// Represents the end-user's full name in displayable form.
    /// </summary>
    public static readonly AttributeDefinition Name = new()
    {
        Code = AttributeCode.Create("name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's full name in displayable form including all name parts, possibly including titles and suffixes, ordered according to the End-User's locale and preferences.")
    };

    /// <summary>
    /// Attribute definition for the <c>given_name</c> OIDC claim.
    /// Represents the end-user's given name(s) or first name(s).
    /// </summary>
    public static readonly AttributeDefinition GivenName = new()
    {
        Code = AttributeCode.Create("given_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Given name(s) or first name(s) of the End-User.")
    };

    /// <summary>
    /// Attribute definition for the <c>family_name</c> OIDC claim.
    /// Represents the end-user's surname(s) or last name(s).
    /// </summary>
    public static readonly AttributeDefinition FamilyName = new()
    {
        Code = AttributeCode.Create("family_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Surname(s) or last name(s) of the End-User.")
    };

    /// <summary>
    /// Attribute definition for the <c>middle_name</c> OIDC claim.
    /// Represents the end-user's middle name(s).
    /// </summary>
    public static readonly AttributeDefinition MiddleName = new()
    {
        Code = AttributeCode.Create("middle_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Middle name(s) of the End-User.")
    };

    /// <summary>
    /// Attribute definition for the <c>nickname</c> OIDC claim.
    /// Represents the end-user's casual name, which may differ from the given name.
    /// </summary>
    public static readonly AttributeDefinition Nickname = new()
    {
        Code = AttributeCode.Create("nickname"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Casual name of the End-User that may or may not be the same as the given_name.")
    };

    /// <summary>
    /// Attribute definition for the <c>preferred_username</c> OIDC claim.
    /// Represents the shorthand name by which the end-user wishes to be referred to at the relying party.
    /// </summary>
    public static readonly AttributeDefinition PreferredUserName = new()
    {
        Code = AttributeCode.Create("preferred_username"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Shorthand name by which the End-User wishes to be referred to at the RP, such as janedoe or j.doe. This value MAY change over time and may not be unique.")
    };

    /// <summary>
    /// Attribute definition for the <c>profile</c> OIDC claim.
    /// Represents the URL of the end-user's profile page.
    /// </summary>
    public static readonly AttributeDefinition Profile = new()
    {
        Code = AttributeCode.Create("profile"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("URL of the End-User's profile page at the RP.")
    };

    /// <summary>
    /// Attribute definition for the <c>picture</c> OIDC claim.
    /// Represents the URL of the end-user's profile picture.
    /// </summary>
    public static readonly AttributeDefinition Picture = new()
    {
        Code = AttributeCode.Create("picture"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("URL of the End-User's profile picture at the RP.")
    };

    /// <summary>
    /// Attribute definition for the <c>website</c> OIDC claim.
    /// Represents the URL of the end-user's personal website or blog.
    /// </summary>
    public static readonly AttributeDefinition Website = new()
    {
        Code = AttributeCode.Create("website"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("URL of the End-User's personal website or blog.")
    };

    /// <summary>
    /// Attribute definition for the <c>email</c> OIDC claim.
    /// Represents the end-user's preferred e-mail address.
    /// </summary>
    public static readonly AttributeDefinition Email = new()
    {
        Code = AttributeCode.Create("email"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's preferred e-mail address at the RP. This value may change over time and may not be unique.")
    };

    /// <summary>
    /// Attribute definition for the <c>email_verified</c> OIDC claim.
    /// Indicates whether the end-user's e-mail address has been verified.
    /// </summary>
    public static readonly AttributeDefinition EmailVerified = new()
    {
        Code = AttributeCode.Create("email_verified"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
        Description = AttributeDescription.Create("Indicates whether the End-User's e-mail address has been verified. The default value is false.")
    };

    /// <summary>
    /// Attribute definition for the <c>gender</c> OIDC claim.
    /// Represents the end-user's gender.
    /// </summary>
    public static readonly AttributeDefinition Gender = new()
    {
        Code = AttributeCode.Create("gender"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's gender.")
    };

    /// <summary>
    /// Attribute definition for the <c>birthdate</c> OIDC claim.
    /// Represents the end-user's birthday.
    /// </summary>
    public static readonly AttributeDefinition Birthdate = new()
    {
        Code = AttributeCode.Create("birthdate"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Date),
        Description = AttributeDescription.Create("End-User's birthday.")
    };

    /// <summary>
    /// Attribute definition for the <c>zoneinfo</c> OIDC claim.
    /// Represents the end-user's time zone, e.g. <c>Europe/Paris</c> or <c>America/Los_Angeles</c>.
    /// </summary>
    public static readonly AttributeDefinition Zoneinfo = new()
    {
        Code = AttributeCode.Create("zoneinfo"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's time zone, such as Europe/Paris or America/Los_Angeles.")
    };

    /// <summary>
    /// Attribute definition for the <c>locale</c> OIDC claim.
    /// Represents the end-user's locale, e.g. <c>en-US</c> or <c>fr-CA</c>.
    /// </summary>
    public static readonly AttributeDefinition Locale = new()
    {
        Code = AttributeCode.Create("locale"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's locale, such as en-US or fr-CA. This value may change over time.")
    };

    /// <summary>
    /// Attribute definition for the <c>phone_number</c> OIDC claim.
    /// Represents the end-user's preferred phone number.
    /// </summary>
    public static readonly AttributeDefinition PhoneNumber = new()
    {
        Code = AttributeCode.Create("phone_number"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's preferred phone number at the RP. This value may change over time and may not be unique.")
    };

    /// <summary>
    /// Attribute definition for the <c>phone_number_verified</c> OIDC claim.
    /// Indicates whether the end-user's phone number has been verified.
    /// </summary>
    public static readonly AttributeDefinition PhoneNumberVerified = new()
    {
        Code = AttributeCode.Create("phone_number_verified"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
        Description = AttributeDescription.Create("Indicates whether the End-User's phone number has been verified. The default value is false.")
    };

    /// <summary>
    /// Attribute definition for the <c>address</c> OIDC claim.
    /// Represents the end-user's preferred postal address.
    /// </summary>
    public static readonly AttributeDefinition Address = new()
    {
        Code = AttributeCode.Create("address"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's preferred postal address at the RP. This value may change over time and may not be unique.")
    };
}

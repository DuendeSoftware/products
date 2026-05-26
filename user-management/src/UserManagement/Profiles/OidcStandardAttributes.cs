// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

public static class OidcStandardAttributes
{
    public static readonly AttributeDefinition Name = new()
    {
        Code = AttributeCode.Create("name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's full name in displayable form including all name parts, possibly including titles and suffixes, ordered according to the End-User's locale and preferences.")
    };

    public static readonly AttributeDefinition GivenName = new()
    {
        Code = AttributeCode.Create("given_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Given name(s) or first name(s) of the End-User.")
    };

    public static readonly AttributeDefinition FamilyName = new()
    {
        Code = AttributeCode.Create("family_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Surname(s) or last name(s) of the End-User.")
    };

    public static readonly AttributeDefinition MiddleName = new()
    {
        Code = AttributeCode.Create("middle_name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Middle name(s) of the End-User.")
    };

    public static readonly AttributeDefinition Nickname = new()
    {
        Code = AttributeCode.Create("nickname"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Casual name of the End-User that may or may not be the same as the given_name.")
    };

    public static readonly AttributeDefinition PreferredUserName = new()
    {
        Code = AttributeCode.Create("preferred_username"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("Shorthand name by which the End-User wishes to be referred to at the RP, such as janedoe or j.doe. This value MAY change over time and may not be unique.")
    };

    public static readonly AttributeDefinition Profile = new()
    {
        Code = AttributeCode.Create("profile"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("URL of the End-User's profile page at the RP.")
    };

    public static readonly AttributeDefinition Picture = new()
    {
        Code = AttributeCode.Create("picture"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("URL of the End-User's profile picture at the RP.")
    };

    public static readonly AttributeDefinition Website = new()
    {
        Code = AttributeCode.Create("website"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("URL of the End-User's personal website or blog.")
    };

    public static readonly AttributeDefinition Email = new()
    {
        Code = AttributeCode.Create("email"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's preferred e-mail address at the RP. This value may change over time and may not be unique.")
    };

    public static readonly AttributeDefinition EmailVerified = new()
    {
        Code = AttributeCode.Create("email_verified"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
        Description = AttributeDescription.Create("Indicates whether the End-User's e-mail address has been verified. The default value is false.")
    };

    public static readonly AttributeDefinition Gender = new()
    {
        Code = AttributeCode.Create("gender"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's gender.")
    };

    public static readonly AttributeDefinition Birthdate = new()
    {
        Code = AttributeCode.Create("birthdate"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Date),
        Description = AttributeDescription.Create("End-User's birthday.")
    };

    public static readonly AttributeDefinition Zoneinfo = new()
    {
        Code = AttributeCode.Create("zoneinfo"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's time zone, such as Europe/Paris or America/Los_Angeles.")
    };

    public static readonly AttributeDefinition Locale = new()
    {
        Code = AttributeCode.Create("locale"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's locale, such as en-US or fr-CA. This value may change over time.")
    };

    public static readonly AttributeDefinition PhoneNumber = new()
    {
        Code = AttributeCode.Create("phone_number"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's preferred phone number at the RP. This value may change over time and may not be unique.")
    };

    public static readonly AttributeDefinition PhoneNumberVerified = new()
    {
        Code = AttributeCode.Create("phone_number_verified"),
        AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
        Description = AttributeDescription.Create("Indicates whether the End-User's phone number has been verified. The default value is false.")
    };

    public static readonly AttributeDefinition Address = new()
    {
        Code = AttributeCode.Create("address"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("End-User's preferred postal address at the RP. This value may change over time and may not be unique.")
    };
}

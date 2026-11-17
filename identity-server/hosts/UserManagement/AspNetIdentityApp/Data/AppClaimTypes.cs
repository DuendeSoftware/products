// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace AspNetIdentityApp.Data;

/// <summary>
/// Custom claim type constants for extra user profile data stored in AspNetUserClaims.
/// These map to SCIM User schema attributes (RFC 7643 §4.1).
/// </summary>
internal static class AppClaimTypes
{
    internal const string GivenName = "given_name";
    internal const string FamilyName = "family_name";
    internal const string MiddleName = "middle_name";
    internal const string DisplayName = "display_name";
    internal const string Title = "title";
    internal const string UserType = "user_type";
    internal const string PreferredLanguage = "preferred_language";
    internal const string Locale = "locale";
    internal const string Timezone = "timezone";
    internal const string PhoneNumber = "phone_number";
    internal const string PhoneNumberType = "phone_number_type";
    internal const string StreetAddress = "street_address";
    internal const string Locality = "locality";
    internal const string Region = "region";
    internal const string PostalCode = "postal_code";
    internal const string Country = "country";
}

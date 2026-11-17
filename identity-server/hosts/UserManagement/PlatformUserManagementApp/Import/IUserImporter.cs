// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace PlatformUserManagementApp.Import;

/// <summary>
/// Imports users from an ASP.NET Identity database into the Duende Platform user store.
/// </summary>
/// <remarks>
/// Mapping:
/// - Identity <c>AspNetUsers.UserName</c> → Platform <c>UserImportRecord.UserName</c>
/// - Identity <c>AspNetUsers.Email</c> → Platform <c>emails</c> complex SCIM attribute + OTP email authenticator
/// - Identity <c>AspNetUserClaims</c> (GivenName, FamilyName, etc.) → Platform SCIM attributes
/// - Identity <c>AspNetUserClaims</c> (StreetAddress, Locality, etc.) → Platform <c>addresses</c> complex SCIM attribute
/// - Identity <c>AspNetUsers.PasswordHash</c> → Platform password authenticator (requires hash migration)
/// - Identity passkey credentials (if stored) → Platform passkey authenticator
/// </remarks>
public interface IUserImporter
{
    /// <summary>
    /// Imports all users from the ASP.NET Identity PostgreSQL database.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<ImportResult> ImportFromIdentityDbAsync(Ct ct);
}

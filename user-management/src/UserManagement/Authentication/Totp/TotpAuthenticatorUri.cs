// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Generates otpauth URIs for TOTP authenticators according to the otpauth URI format specification.
/// </summary>
/// <remarks>
/// See https://www.ietf.org/archive/id/draft-linuxgemini-otpauth-uri-00.html
/// </remarks>
public static class TotpAuthenticatorUri
{
    // Format: otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&digits=6
    // Note: {0} (issuer) appears twice as required by the otpauth URI spec - once in the label and once as a query parameter
    private static readonly CompositeFormat UriFormat = CompositeFormat.Parse("otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6");

    /// <summary>
    /// Generates an otpauth URI for use with authenticator apps.
    /// </summary>
    /// <param name="issuer">The issuer name (e.g., application or company name). Will be URL-encoded.</param>
    /// <param name="accountIdentifier">The account identifier (typically email or username). Will be URL-encoded.</param>
    /// <param name="key">The TOTP key to encode in the URI.</param>
    /// <returns>A complete otpauth:// URI string suitable for QR code generation.</returns>
    public static string Generate(string issuer, string accountIdentifier, PlainBytesTotpKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountIdentifier);

        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountIdentifier);
        var base32Secret = key.EncodeToBase32();

        return string.Format(
            CultureInfo.InvariantCulture,
            UriFormat,
            encodedIssuer,
            encodedAccount,
            base32Secret);
    }
}

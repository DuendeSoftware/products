// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal;

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Represents a plain text TOTP (Time-based One-Time Password) code submitted by a user for verification.
/// </summary>
[StringValue]
public partial record PlainTextTotp
{
    internal static byte Length => 6;

    /// <summary>Gets the normalized TOTP code string value.</summary>
    public string Value { get; }

    /// <summary>Returns a redacted string to prevent accidental logging of TOTP values.</summary>
    public override string ToString() => GetType().ToString();

    static string Normalize(string value) =>
        Base32Crockford.Normalize(value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal));

    static bool TryValidate(string s, out IReadOnlyList<string>? errors)
    {
        errors = null;
        if (!Base32Crockford.IsValid(s, Length))
        {
            errors = ["The value is not a valid Base32 Crockford string."];
            return false;
        }

        return true;
    }
}

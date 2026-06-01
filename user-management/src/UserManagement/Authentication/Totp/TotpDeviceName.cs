// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Strongly-typed name for a TOTP device registered by a user (e.g., "Default", "Work Phone").
/// </summary>
[StringValue]
public partial record TotpDeviceName
{
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous values,
    // so we may as well use some reasonable number.
    internal const int MaxLength = 255;

    /// <summary>Gets the string value of the authenticator name.</summary>
    public string Value { get; }

    /// <summary>The default TOTP authenticator name.</summary>
    public static TotpDeviceName Default { get; } = Create("Default");

    static string Normalize(string value) => value.Trim();

    internal static TotpDeviceName Load(string value) => new(value);
}

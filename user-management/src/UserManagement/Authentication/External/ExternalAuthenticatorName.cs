// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.External;

/// <summary>
/// Strongly-typed name for an external authentication provider (e.g., "Google", "GitHub").
/// </summary>
[StringValue]
public partial record ExternalAuthenticatorName
{
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous values,
    // so we may as well use some reasonable number.
    internal const int MaxLength = 255;

    /// <summary>Gets the string value of the provider name.</summary>
    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static ExternalAuthenticatorName Load(string value) => new(value);
}

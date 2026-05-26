// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.External;

[StringValue]
public partial record ExternalAuthenticatorName
{
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous values,
    // so we may as well use some reasonable number.
    internal const int MaxLength = 255;

    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static ExternalAuthenticatorName Load(string value) => new(value);
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

[StringValue]
public partial record UserName
{
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous values,
    // so we may as well use some reasonable number.
    // Some systems may use an email address as the username,
    // so the max length must be at least as large as the max length
    // of an email address (320 chars).
    internal const int MaxLength = 320;

    private static string Normalize(string input) => input.Trim();
}

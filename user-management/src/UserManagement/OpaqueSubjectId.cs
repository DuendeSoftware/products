// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

// https://www.rfc-editor.org/rfc/rfc9493.html#name-opaque-identifier-format
[StringValue]
public partial record OpaqueSubjectId : ISubjectId
{
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous values,
    // so we may as well use some reasonable number.
    internal const int MaxLength = 255;

    private static string Normalize(string input) => input.Trim();


}

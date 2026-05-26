// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.UserManagement.Membership;

[StringValue]
public partial record RoleId
{
    internal const int MaxLength = 200;

    [GeneratedRegex(@"^[a-zA-Z0-9\-_/\\]+$")]
    internal static partial Regex Regex();

    public static RoleId New() => Create(Guid.NewGuid().ToString());
}

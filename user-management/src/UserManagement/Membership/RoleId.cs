// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.UserManagement.Membership;

/// <summary>Represents a unique identifier for a role.</summary>
[StringValue]
public partial record RoleId
{
    internal const int MaxLength = 200;

    [GeneratedRegex(@"^[a-zA-Z0-9\-_/\\]+$")]
    internal static partial Regex Regex();

    /// <summary>Creates a new <see cref="RoleId"/> with a randomly generated value.</summary>
    public static RoleId New() => Create(Guid.NewGuid().ToString());
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.UserManagement.Membership;

/// <summary>Represents a unique identifier for a group.</summary>
[StringValue]
public partial record GroupId
{
    internal const int MaxLength = 200;

    [GeneratedRegex(@"^[a-zA-Z0-9\-_/\\]+$")]
    internal static partial Regex Regex();

    /// <summary>Creates a new <see cref="GroupId"/> with a randomly generated value.</summary>
    public static GroupId New() => Create(Guid.NewGuid().ToString());
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>Represents a human-readable description of a group.</summary>
[StringValue]
public partial record GroupDescription
{
    internal const int MaxLength = 500;

    /// <summary>Gets the normalized string value of the group description.</summary>
    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static GroupDescription Load(string value) => new(value);
}

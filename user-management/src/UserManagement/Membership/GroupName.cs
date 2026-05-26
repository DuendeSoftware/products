// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

[StringValue]
public partial record GroupName
{
    internal const int MaxLength = 200;

    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static GroupName Load(string value) => new(value);
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

[StringValue]
public partial record GroupDescription
{
    internal const int MaxLength = 500;

    public string Value { get; }

    static string Normalize(string value) => value.Trim();

    internal static GroupDescription Load(string value) => new(value);
}

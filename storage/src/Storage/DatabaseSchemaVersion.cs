// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

public sealed record DatabaseSchemaVersion
{
    public int Value { get; }

    public DatabaseSchemaVersion(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public static readonly DatabaseSchemaVersion Zero = new(0);
}

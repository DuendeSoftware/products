// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.DynamicFrontends;

public sealed record BffProxy
{
    public RemoteApi[] RemoteApis { get; init; } = [];

    public bool Equals(BffProxy? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return RemoteApis.SequenceEqual(other.RemoteApis);
    }

    public override int GetHashCode() => RemoteApis.GetHashCode();
}

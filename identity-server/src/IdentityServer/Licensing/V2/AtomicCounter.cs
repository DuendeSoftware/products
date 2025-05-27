// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Licensing.V2;

internal class AtomicCounter(int initialCount = 0)
{
    private long _count = initialCount;

    public void Increment() => Interlocked.Increment(ref _count);

    public long Count => _count;
}

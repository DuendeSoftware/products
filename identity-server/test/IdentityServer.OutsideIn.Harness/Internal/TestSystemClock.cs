// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Internal;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// The test clock as the <see cref="ISystemClock" /> that <c>MemoryCache</c> and <c>MemoryDistributedCache</c> expire
/// entries by (design doc D15). They don't read <see cref="TimeProvider" />, so without this, advancing the test clock
/// would never expire a cached entry.
/// </summary>
internal sealed class TestSystemClock(TestClock clock) : ISystemClock
{
    public DateTimeOffset UtcNow => clock.TimeProvider.GetUtcNow();
}

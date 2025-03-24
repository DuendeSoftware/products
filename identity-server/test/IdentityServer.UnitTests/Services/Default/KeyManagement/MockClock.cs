// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;

namespace UnitTests.Services.Default.KeyManagement;

internal class MockClock : IClock
{
    public MockClock()
    {
    }

    public MockClock(DateTime now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; set; }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.v2;

namespace UnitTests.Common;

public class TestProtocolRequestCounter : IProtocolRequestCounter
{
    public void Increment()
    {
        RequestCount++;
    }

    public uint RequestCount { get; set; }
}
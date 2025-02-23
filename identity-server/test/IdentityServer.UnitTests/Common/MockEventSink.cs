// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Threading.Tasks;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

internal class MockEventSink : IEventSink
{
    public List<Event> Events { get; } = [];

    public Task PersistAsync(Event evt)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }
}
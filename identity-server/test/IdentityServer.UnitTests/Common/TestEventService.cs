// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTests.Common;

public class TestEventService : IEventService
{
    private Dictionary<Type, object> _events = new Dictionary<Type, object>();

    public Task RaiseAsync(Event evt)
    {
        _events.Add(evt.GetType(), evt);
        return Task.CompletedTask;
    }

    public T AssertEventWasRaised<T>()
        where T : class
    {
        _events.ContainsKey(typeof(T)).Should().BeTrue();
        return (T)_events.Where(x => x.Key == typeof(T)).Select(x => x.Value).First();
    }

    public bool CanRaiseEventType(EventTypes evtType)
    {
        return true;
    }
}

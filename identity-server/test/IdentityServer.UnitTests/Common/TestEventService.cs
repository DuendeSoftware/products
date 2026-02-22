// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class TestEventService : IEventService
{
    private Dictionary<Type, object> _events = new Dictionary<Type, object>();

    public Task RaiseAsync(Event evt, Ct ct)
    {
        _events.Add(evt.GetType(), evt);
        return Task.CompletedTask;
    }

    public T AssertEventWasRaised<T>()
        where T : class
    {
        _events.ContainsKey(typeof(T)).ShouldBeTrue();
        return (T)_events.Where(x => x.Key == typeof(T)).Select(x => x.Value).First();
    }

    public void AssertEventWasNotRaised<T>()
        where T : class => _events.ShouldNotContainKey(typeof(T));

    public bool CanRaiseEventType(EventTypes evtType) => true;
}

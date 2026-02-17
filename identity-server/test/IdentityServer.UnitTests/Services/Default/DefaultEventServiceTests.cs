// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Events;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;
using UnitTests.Services.Default.KeyManagement;

namespace UnitTests.Services.Default;

public class DefaultEventServiceTests
{
    [Fact]
    public async Task Raising_an_event_without_http_context_does_not_throw()
    {
        var options = new IdentityServerOptions();
        options.Events.RaiseInformationEvents = true;

        var sink = new MockEventSink();

        var sut = new DefaultEventService(
            options,
            // This is the most important part of this test. We want to ensure
            // that we don't throw exceptions when there is no http context available.
            new NullHttpContextAccessor(),
            sink,
            new FakeTimeProvider());

        var evt = new TestEvent(id: 123);

        await sut.RaiseAsync(evt);

        sink.Events.ShouldContain(e => e.Id == 123);
    }


}

internal class TestEvent : Event
{
    public TestEvent(int id = 0, string message = "")
        : base(category: "Test", name: "Test", EventTypes.Information, id, message)
    {
    }
}

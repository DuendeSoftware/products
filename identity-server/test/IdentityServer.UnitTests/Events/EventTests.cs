// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Events;

namespace UnitTests.Endpoints.Results;

public class EventTests
{

    [Fact]
    public void UnhandledExceptionEventCanCallToString()
    {
        try
        {
            throw new InvalidOperationException("Boom");
        }
        catch (Exception ex)
        {
            var unhandledExceptionEvent = new UnhandledExceptionEvent(ex);

            var s = unhandledExceptionEvent.ToString();

            s.ShouldNotBeNullOrEmpty();
        }
    }
}

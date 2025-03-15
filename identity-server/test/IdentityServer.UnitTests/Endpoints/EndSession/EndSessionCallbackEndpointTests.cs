// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Endpoints;
using Microsoft.Extensions.Logging;

namespace UnitTests.Endpoints.EndSession;

public class EndSessionCallbackEndpointTests
{
    private const string Category = "End Session Callback Endpoint";

    private StubEndSessionRequestValidator _stubEndSessionRequestValidator = new StubEndSessionRequestValidator();
    private EndSessionCallbackEndpoint _subject;

    public EndSessionCallbackEndpointTests()
    {
        _subject = new EndSessionCallbackEndpoint(
            _stubEndSessionRequestValidator,
            new LoggerFactory().CreateLogger<EndSessionCallbackEndpoint>());
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.IntegrationTests.Endpoints.EndSession;

/// <summary>
/// Runs end session endpoint protocol tests against the default in-memory stores.
/// </summary>
public sealed class EndSessionTests : EndSessionTestsBase
{
    public EndSessionTests() => InitializePipeline();
}

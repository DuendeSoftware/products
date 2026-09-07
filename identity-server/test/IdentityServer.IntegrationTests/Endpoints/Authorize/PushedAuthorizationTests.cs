// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Authorize;

/// <summary>
/// Runs pushed authorization protocol tests against the default in-memory stores.
/// </summary>
public sealed class PushedAuthorizationTests : PushedAuthorizationTestsBase
{
    public PushedAuthorizationTests() => InitializePipeline();
}

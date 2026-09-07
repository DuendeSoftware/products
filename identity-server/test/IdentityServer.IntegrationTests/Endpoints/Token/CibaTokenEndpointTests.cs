// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Token;

/// <summary>
/// Runs CIBA token endpoint protocol tests against the default in-memory stores.
/// </summary>
public sealed class CibaTokenEndpointTests : CibaTokenEndpointTestsBase
{
    public CibaTokenEndpointTests() => InitializePipeline();
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Ciba;

/// <summary>
/// Runs CIBA backchannel authentication endpoint tests against the default in-memory stores.
/// </summary>
public sealed class CibaTests : CibaTestsBase
{
    public CibaTests() => InitializePipeline();
}

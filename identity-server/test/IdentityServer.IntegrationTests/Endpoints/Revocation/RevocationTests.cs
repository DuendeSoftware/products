// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Revocation;

/// <summary>
/// Runs revocation endpoint protocol tests against the default in-memory stores.
/// </summary>
public sealed class RevocationTests : RevocationTestsBase
{
    public RevocationTests() => InitializePipeline();
}

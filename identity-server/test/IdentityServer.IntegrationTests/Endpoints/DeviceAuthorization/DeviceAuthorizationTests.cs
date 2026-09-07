// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.IntegrationTests.Endpoints.DeviceAuthorization;

/// <summary>
/// Runs device authorization protocol tests against the default in-memory stores.
/// </summary>
public sealed class DeviceAuthorizationTests : DeviceAuthorizationTestsBase
{
    public DeviceAuthorizationTests() => _mockPipeline.Initialize();
}

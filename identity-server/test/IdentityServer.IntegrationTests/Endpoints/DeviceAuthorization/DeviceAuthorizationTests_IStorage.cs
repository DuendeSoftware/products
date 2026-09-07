// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.DeviceAuthorization;

/// <summary>
/// Runs device authorization protocol tests against IStorage-backed operational
/// stores using an in-memory SQLite database.
/// </summary>
public sealed class DeviceAuthorizationTests_IStorage : DeviceAuthorizationTestsBase, IAsyncLifetime
{
    public DeviceAuthorizationTests_IStorage()
    {
        _mockPipeline.OnPostConfigureServices += services => services.AddOperationalStorageForTesting();
        _mockPipeline.Initialize();
    }

    public async ValueTask InitializeAsync() =>
        await _mockPipeline.MigrateStorageSchemaAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

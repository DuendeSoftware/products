// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Authorize;

/// <summary>
/// Runs pushed authorization protocol tests against IStorage-backed operational
/// stores using an in-memory SQLite database.
/// </summary>
public sealed class PushedAuthorizationTests_IStorage : PushedAuthorizationTestsBase, IAsyncLifetime
{
    public PushedAuthorizationTests_IStorage()
    {
        _mockPipeline.OnPostConfigureServices += services => services.AddOperationalStorageForTesting();
        InitializePipeline();
    }

    public async ValueTask InitializeAsync() =>
        await _mockPipeline.MigrateStorageSchemaAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

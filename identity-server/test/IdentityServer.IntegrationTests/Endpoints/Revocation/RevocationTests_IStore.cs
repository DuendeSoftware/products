// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Revocation;

/// <summary>
/// Runs revocation endpoint protocol tests against IStore-backed operational
/// stores using an in-memory SQLite database.
/// </summary>
public sealed class RevocationTests_IStore : RevocationTestsBase, IAsyncLifetime
{
    public RevocationTests_IStore()
    {
        _mockPipeline.OnPostConfigureServices += services => services.AddOperationalStorageForTesting();
        InitializePipeline();
    }

    public async ValueTask InitializeAsync() =>
        await _mockPipeline.MigrateStorageSchemaAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

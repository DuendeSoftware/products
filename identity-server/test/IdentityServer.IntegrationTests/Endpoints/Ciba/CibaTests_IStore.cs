// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Ciba;

/// <summary>
/// Runs CIBA backchannel authentication endpoint tests against IStore-backed
/// operational stores using an in-memory SQLite database.
/// </summary>
public sealed class CibaTests_IStore : CibaTestsBase, IAsyncLifetime
{
    public CibaTests_IStore()
    {
        _mockPipeline.OnPostConfigureServices += services => services.AddOperationalStorageForTesting();
        InitializePipeline();
    }

    public async ValueTask InitializeAsync() =>
        await _mockPipeline.MigrateStorageSchemaAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

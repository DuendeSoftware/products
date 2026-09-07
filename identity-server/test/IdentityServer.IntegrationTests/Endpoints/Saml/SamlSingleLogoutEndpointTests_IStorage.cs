// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

/// <summary>
/// Runs SAML single logout endpoint tests against IStorage-backed operational
/// stores using an in-memory SQLite database.
/// </summary>
public sealed class SamlSingleLogoutEndpointTests_IStorage : SamlSingleLogoutEndpointTestsBase
{
    public SamlSingleLogoutEndpointTests_IStorage()
    {
        Fixture.ConfigureServices = services => services.AddOperationalStorageForTesting();
        Fixture.OnPostInitialize = pipeline => pipeline.MigrateStorageSchemaAsync();
    }
}

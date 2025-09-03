// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.EntityFrameworkCore.Storage;

namespace IntegrationTests.TestHosts;

public class ConfigurationIntegrationTestBase
{
    protected readonly IdentityServerHost IdentityServerHost;
    protected readonly ConfigurationHost ConfigurationHost;

    public ConfigurationIntegrationTestBase()
    {
        var dbRoot = new InMemoryDatabaseRoot();
        IdentityServerHost = new IdentityServerHost(dbRoot);
        IdentityServerHost.InitializeAsync().Wait();

        ConfigurationHost = new ConfigurationHost(dbRoot);
        ConfigurationHost.InitializeAsync().Wait();
    }
}

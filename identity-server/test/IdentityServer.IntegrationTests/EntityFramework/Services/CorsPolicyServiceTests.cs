// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Services;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.Services;

public class CorsPolicyServiceTests : IntegrationTest<CorsPolicyServiceTests, ConfigurationDbContext, ConfigurationStoreOptions>
{
    private readonly CT _ct = TestContext.Current.CancellationToken;

    public CorsPolicyServiceTests(DatabaseProviderFixture<ConfigurationDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new ConfigurationDbContext(options);
            context.Database.EnsureCreated();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task IsOriginAllowedAsync_WhenOriginIsAllowed_ExpectTrue(DbContextOptions<ConfigurationDbContext> options)
    {
        const string testCorsOrigin = "https://identityserver.io/";

        await using (var context = new ConfigurationDbContext(options))
        {
            context.Clients.Add(new Client
            {
                ClientId = Guid.NewGuid().ToString(),
                ClientName = Guid.NewGuid().ToString(),
                AllowedCorsOrigins = new List<string> { "https://www.identityserver.com" }
            }.ToEntity());
            context.Clients.Add(new Client
            {
                ClientId = "2",
                ClientName = "2",
                AllowedCorsOrigins = new List<string> { "https://www.identityserver.com", testCorsOrigin }
            }.ToEntity());
            await context.SaveChangesAsync();
        }

        bool result;
        await using (var context = new ConfigurationDbContext(options))
        {
            var service = new CorsPolicyService(context, new NullLogger<CorsPolicyService>(), new NoneCancellationTokenProvider());
            result = await service.IsOriginAllowedAsync(testCorsOrigin, _ct);
        }

        result.ShouldBeTrue();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task IsOriginAllowedAsync_WhenOriginIsNotAllowed_ExpectFalse(DbContextOptions<ConfigurationDbContext> options)
    {
        await using (var context = new ConfigurationDbContext(options))
        {
            context.Clients.Add(new Client
            {
                ClientId = Guid.NewGuid().ToString(),
                ClientName = Guid.NewGuid().ToString(),
                AllowedCorsOrigins = new List<string> { "https://www.identityserver.com" }
            }.ToEntity());
            await context.SaveChangesAsync();
        }

        bool result;
        await using (var context = new ConfigurationDbContext(options))
        {
            var service = new CorsPolicyService(context, new NullLogger<CorsPolicyService>(), new NoneCancellationTokenProvider());
            result = await service.IsOriginAllowedAsync("InvalidOrigin", _ct);
        }

        result.ShouldBeFalse();
    }
}

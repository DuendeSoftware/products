// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// The Entity Framework store type in its split layout (design doc <c>EntityFrameworkStoreProvider</c>, D29):
/// configuration data in one SQLite database, operational data in another, both shared by every node. Writes go
/// through the DbContexts, with IdentityServer's own entity mappers. Caching (design doc D34) wraps the
/// configuration stores: with <see cref="CachingMode.InMemory" /> each node gets its own cache; with
/// <see cref="CachingMode.Distributed" /> both nodes share one <see cref="IDistributedCache" /> instance.
/// </summary>
/// <remarks>
/// The schema is created with <c>EnsureCreated</c> from the DbContext models, not with migrations: the repository only
/// has SQL Server migrations, and the design's embedded SQLite migrations (D21) don't exist yet.
/// </remarks>
internal sealed class EntityFrameworkStoreProvider : DatabaseStoreProvider
{
    private readonly CachingMode _caching;
    private readonly IDistributedCache? _distributedCache;
    private readonly TestDatabaseDirectory _directory = new();
    private readonly TestDatabase _configuration;
    private readonly TestDatabase _operational;

    public EntityFrameworkStoreProvider(CachingMode caching, IDistributedCache? distributedCache)
    {
        _caching = caching;
        _distributedCache = distributedCache;
        _configuration = _directory.Database("configuration");
        _operational = _directory.Database("operational");
    }

    public override void ConfigureNode(IIdentityServerBuilder builder, int nodeIndex)
    {
        builder
            .AddEntityFrameworkConfigurationStore(options => options.ConfigureDbContext = db => db.UseSqlite(_configuration.ConnectionString))
            .AddEntityFrameworkOperationalStore(options =>
            {
                options.ConfigureDbContext = db => db.UseSqlite(_operational.ConnectionString);
                options.EnableTokenCleanup = false;
            });

        if (_caching != CachingMode.None)
        {
            builder.Services.UseCacheTiers(_distributedCache);
            builder.AddConfigurationStoreCache();
        }
    }

    public override Task AddClientAsync(Client client, Ct cancellationToken) =>
        Configuration(context => context.Clients.Add(client.ToEntity()), cancellationToken);

    public override async Task UpdateClientAsync(Client client, Ct cancellationToken)
    {
        await RemoveClientAsync(client.ClientId, cancellationToken);
        await AddClientAsync(client, cancellationToken);
    }

    public override Task RemoveClientAsync(string clientId, Ct cancellationToken) =>
        InScopeAsync(services => services.GetRequiredService<ConfigurationDbContext>().Clients
            .Where(client => client.ClientId == clientId)
            .ExecuteDeleteAsync(cancellationToken));

    public override Task AddIdentityResourceAsync(IdentityResource resource, Ct cancellationToken) =>
        Configuration(context => context.IdentityResources.Add(resource.ToEntity()), cancellationToken);

    public override Task<bool> HasIdentityResourceAsync(string name, Ct cancellationToken) =>
        InScopeAsync(services => services.GetRequiredService<ConfigurationDbContext>().IdentityResources
            .AnyAsync(resource => resource.Name == name, cancellationToken));

    public override Task AddApiScopeAsync(ApiScope scope, Ct cancellationToken) =>
        Configuration(context => context.ApiScopes.Add(scope.ToEntity()), cancellationToken);

    public override Task AddApiResourceAsync(ApiResource resource, Ct cancellationToken) =>
        Configuration(context => context.ApiResources.Add(resource.ToEntity()), cancellationToken);

    public override Task AddIdentityProviderAsync(OidcProvider provider, Ct cancellationToken) =>
        Configuration(context => context.IdentityProviders.Add(provider.ToEntity()), cancellationToken);

    public override ValueTask DisposeAsync()
    {
        _directory.Dispose();
        return ValueTask.CompletedTask;
    }

    protected override async Task CreateSchemaAsync(IServiceProvider scopedServices, HarnessLog log, Ct cancellationToken)
    {
        using (log.Measure($"EF schema for {_configuration.Name} database (EnsureCreated)"))
        {
            await scopedServices.GetRequiredService<ConfigurationDbContext>().Database.EnsureCreatedAsync(cancellationToken);
        }

        using (log.Measure($"EF schema for {_operational.Name} database (EnsureCreated)"))
        {
            await scopedServices.GetRequiredService<PersistedGrantDbContext>().Database.EnsureCreatedAsync(cancellationToken);
        }
    }

    private Task Configuration(Action<ConfigurationDbContext> write, Ct cancellationToken) =>
        InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ConfigurationDbContext>();
            write(context);
            await context.SaveChangesAsync(cancellationToken);
        });
}

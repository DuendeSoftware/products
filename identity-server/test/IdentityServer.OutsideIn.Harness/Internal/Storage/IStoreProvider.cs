// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// One store type for a topology (design doc section 05, <c>IStoreProvider</c>): it wires IdentityServer's stores into
/// each node, prepares its databases before the nodes take traffic, and performs the writes <see cref="ITestStorage" />
/// asks for. Users are not part of it: without User Management they are always IdentityServer test users (D20), kept
/// in memory on every node.
/// </summary>
internal interface IStoreProvider : IAsyncDisposable
{
    /// <summary>Registers the stores in one node.</summary>
    void ConfigureNode(IIdentityServerBuilder builder, int nodeIndex);

    /// <summary>
    /// Prepares the databases (schema creation or migration), once per topology, with the first node's services, before
    /// any node starts. From then on, writes go through those services.
    /// </summary>
    Task InitializeAsync(IServiceProvider services, HarnessLog log, Ct cancellationToken);

    Task AddClientAsync(Client client, Ct cancellationToken);

    Task UpdateClientAsync(Client client, Ct cancellationToken);

    Task RemoveClientAsync(string clientId, Ct cancellationToken);

    Task AddIdentityResourceAsync(IdentityResource resource, Ct cancellationToken);

    Task<bool> HasIdentityResourceAsync(string name, Ct cancellationToken);

    Task AddApiScopeAsync(ApiScope scope, Ct cancellationToken);

    Task AddApiResourceAsync(ApiResource resource, Ct cancellationToken);

    Task AddIdentityProviderAsync(OidcProvider provider, Ct cancellationToken);
}

/// <summary>Shared plumbing for the providers that write through a started node's services.</summary>
internal abstract class DatabaseStoreProvider : IStoreProvider
{
    private IServiceProvider? _services;

    public abstract void ConfigureNode(IIdentityServerBuilder builder, int nodeIndex);

    public async Task InitializeAsync(IServiceProvider services, HarnessLog log, Ct cancellationToken)
    {
        _services = services;
        await using var scope = services.CreateAsyncScope();
        await CreateSchemaAsync(scope.ServiceProvider, log, cancellationToken);
    }

    public abstract Task AddClientAsync(Client client, Ct cancellationToken);

    public abstract Task UpdateClientAsync(Client client, Ct cancellationToken);

    public abstract Task RemoveClientAsync(string clientId, Ct cancellationToken);

    public abstract Task AddIdentityResourceAsync(IdentityResource resource, Ct cancellationToken);

    public abstract Task<bool> HasIdentityResourceAsync(string name, Ct cancellationToken);

    public abstract Task AddApiScopeAsync(ApiScope scope, Ct cancellationToken);

    public abstract Task AddApiResourceAsync(ApiResource resource, Ct cancellationToken);

    public abstract Task AddIdentityProviderAsync(OidcProvider provider, Ct cancellationToken);

    public abstract ValueTask DisposeAsync();

    protected abstract Task CreateSchemaAsync(IServiceProvider scopedServices, HarnessLog log, Ct cancellationToken);

    /// <summary>Runs <paramref name="write" /> in a new DI scope of the first node.</summary>
    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> write)
    {
        var services = _services
            ?? throw new InvalidOperationException("Data can only be written to a database store after the server has started.");
        await using var scope = services.CreateAsyncScope();
        return await write(scope.ServiceProvider);
    }

    protected Task InScopeAsync(Func<IServiceProvider, Task> write) =>
        InScopeAsync<bool>(async scoped =>
        {
            await write(scoped);
            return true;
        });
}

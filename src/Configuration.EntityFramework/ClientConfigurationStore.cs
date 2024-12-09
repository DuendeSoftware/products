// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Communicates with the client configuration data store using entity
/// framework. 
/// </summary>
public class ClientConfigurationStore : IClientConfigurationStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IConfigurationDbContext DbContext;

    /// <summary>
    /// The CancellationToken provider.
    /// </summary>
    protected readonly ICancellationTokenProvider CancellationTokenProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<ClientConfigurationStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientConfigurationStore"/>
    /// class.
    /// </summary>
    public ClientConfigurationStore(
        IConfigurationDbContext dbContext,
        ICancellationTokenProvider cancellationTokenProvider,
        ILogger<ClientConfigurationStore> logger)
    {
        DbContext = dbContext;
        CancellationTokenProvider = cancellationTokenProvider;
        Logger = logger;
    }

    /// <inheritdoc />
    public Task AddAsync(Client client)
    {
        Logger.LogDebug("Adding client {ClientId} to configuration store", client.ClientId);
        DbContext.Clients.Add(client.ToEntity());
        return DbContext.SaveChangesAsync(CancellationTokenProvider.CancellationToken);
    }
}

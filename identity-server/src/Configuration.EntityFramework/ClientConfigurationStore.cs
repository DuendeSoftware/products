// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
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
    /// The logger.
    /// </summary>
    protected readonly ILogger<ClientConfigurationStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientConfigurationStore"/>
    /// class.
    /// </summary>
    public ClientConfigurationStore(
        IConfigurationDbContext dbContext,
        ILogger<ClientConfigurationStore> logger)
    {
        DbContext = dbContext;
        Logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(Client client, CT ct)
    {
        Logger.LogDebug("Adding client {ClientId} to configuration store", client.ClientId);
        DbContext.Clients.Add(client.ToEntity());
        await DbContext.SaveChangesAsync(ct);
    }
}

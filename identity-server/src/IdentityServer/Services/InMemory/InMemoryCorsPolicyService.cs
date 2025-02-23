// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Duende.IdentityServer.Services;

/// <summary>
/// An ICorsPolicyService for use with clients configured with AddInMemoryClients.
/// This service will allow any origin included in any client's AllowedCorsOrigins.
/// </summary>
public class InMemoryCorsPolicyService : ICorsPolicyService
{
    /// <summary>
    /// Logger
    /// </summary>
    protected readonly ILogger Logger;
    /// <summary>
    /// Clients applications list
    /// </summary>
    protected readonly IEnumerable<Client> Clients;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCorsPolicyService"/> class.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="clients">The clients.</param>
    public InMemoryCorsPolicyService(ILogger<InMemoryCorsPolicyService> logger, IEnumerable<Client> clients)
    {
        Logger = logger;
        Clients = clients ?? Enumerable.Empty<Client>();
    }

    /// <summary>
    /// Determines whether origin is allowed.
    /// </summary>
    /// <param name="origin">The origin.</param>
    /// <returns></returns>
    public virtual Task<bool> IsOriginAllowedAsync(string origin)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("InMemoryCorsPolicyService.IsOriginAllowedAsync");
        
        var query =
            from client in Clients
            from url in client.AllowedCorsOrigins
            select url.GetOrigin();

        var result = query.Contains(origin, StringComparer.OrdinalIgnoreCase);

        if (result)
        {
            Logger.LogDebug("Client list checked and origin: {0} is allowed", origin);
        }
        else
        {
            Logger.LogDebug("Client list checked and origin: {0} is not allowed", origin);
        }

        return Task.FromResult(result);
    }
}

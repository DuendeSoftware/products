// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerHost.Pages.Portal;

public class ThirdPartyInitiatedLoginLink
{
    public string? LinkText { get; set; }
    public string? InitiateLoginUri { get; set; }
}


public class ClientRepository
{
    private readonly ConfigurationDbContext _context;

    public ClientRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ThirdPartyInitiatedLoginLink>> GetClientsWithLoginUris(string? filter = null)
    {
        var query = _context.Clients
            .Where(c => c.InitiateLoginUri != null);

        if (!String.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.ClientId.Contains(filter) || x.ClientName.Contains(filter));
        }

        var result = query.Select(c => new ThirdPartyInitiatedLoginLink
        {
            LinkText = string.IsNullOrWhiteSpace(c.ClientName) ? c.ClientId : c.ClientName,
            InitiateLoginUri = c.InitiateLoginUri
        });

        return await result.ToArrayAsync();
    }
}

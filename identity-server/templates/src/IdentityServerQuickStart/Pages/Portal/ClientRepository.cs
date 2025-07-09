using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerQuickStart.Pages.Portal;

public class ThirdPartyInitiatedLoginLink
{
    public string? LinkText { get; set; }
    public string? InitiateLoginUri { get; set; }
    public string? LogoUri { get; set; }
}

public class ClientRepository(ConfigurationDbContext context)
{
    private readonly ConfigurationDbContext _context = context;

    public async Task<IEnumerable<ThirdPartyInitiatedLoginLink>> GetClientsWithLoginUris(string? filter = null)
    {
        var query = _context.Clients
            .Where(c => c.InitiateLoginUri != null);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.ClientId.Contains(filter) || x.ClientName.Contains(filter));
        }

        var result = query.Select(c => new ThirdPartyInitiatedLoginLink
        {
            LinkText = string.IsNullOrWhiteSpace(c.ClientName) ? c.ClientId : c.ClientName,
            InitiateLoginUri = c.InitiateLoginUri,
            LogoUri = c.LogoUri
        });

        return await result.ToArrayAsync();
    }
}

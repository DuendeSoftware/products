using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerHost.Pages.Admin.Clients;

public class ClientSummaryModel
{
    [Required]
    public string ClientId { get; set; } = default!;
    public string? Name { get; set; }
    [Required]
    public Flow Flow { get; set; }
}

public class CreateClientModel : ClientSummaryModel
{
    public string? Secret { get; set; }
    public bool RequireConsent { get; set; } = true;
}

public class EditClientModel : CreateClientModel, IValidatableObject
{
    [Required]
    public List<string> AllowedScopes { get; set; } = [];

    public string? RedirectUri { get; set; }
    public string? InitiateLoginUri { get; set; }
    public string? PostLogoutRedirectUri { get; set; }
    public string? FrontChannelLogoutUri { get; set; }
    public string? BackChannelLogoutUri { get; set; }

    private static readonly string[] memberNames = new[] { "RedirectUri" };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = new List<ValidationResult>();

        if (Flow == Flow.CodeFlowWithPkce)
        {
            if (RedirectUri == null)
            {
                errors.Add(new ValidationResult("Redirect URI is required.", memberNames));
            }
        }

        return errors;
    }
}

public enum Flow
{
    ClientCredentials,
    CodeFlowWithPkce
}

public class ClientRepository
{
    private readonly ConfigurationDbContext _context;

    public ClientRepository(ConfigurationDbContext context) => _context = context;

    public async Task<IEnumerable<ClientSummaryModel>> GetAllAsync(string? filter = null)
    {
        var grants = new[] { GrantType.AuthorizationCode, GrantType.ClientCredentials };

        var query = _context.Clients
            .Include(x => x.AllowedGrantTypes)
            .Where(x => x.AllowedGrantTypes.Count == 1 && x.AllowedGrantTypes.Any(grant => grants.Contains(grant.GrantType)));

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.ClientId.Contains(filter) || x.ClientName.Contains(filter));
        }

        var result = query.Select(x => new ClientSummaryModel
        {
            ClientId = x.ClientId,
            Name = x.ClientName,
            Flow = x.AllowedGrantTypes.Select(x => x.GrantType).Single() == GrantType.ClientCredentials ? Flow.ClientCredentials : Flow.CodeFlowWithPkce
        });

        return await result.ToArrayAsync();
    }

    public async Task<EditClientModel?> GetByIdAsync(string id)
    {
        var client = await _context.Clients
            .Include(x => x.AllowedGrantTypes)
            .Include(x => x.AllowedScopes)
            .Include(x => x.RedirectUris)
            .Include(x => x.PostLogoutRedirectUris)
            .Where(x => x.ClientId == id)
            .SingleOrDefaultAsync();

        if (client == null)
        {
            return null;
        }

        return new EditClientModel
        {
            ClientId = client.ClientId,
            Name = client.ClientName,
            Flow = client.AllowedGrantTypes
                                       .Select(x => x.GrantType)
                                       .Single() == GrantType.ClientCredentials
                                     ? Flow.ClientCredentials
                                     : Flow.CodeFlowWithPkce,
            AllowedScopes = [.. client.AllowedScopes.Select(x => x.Scope)],
            RedirectUri = client.RedirectUris
                                       .Select(x => x.RedirectUri)
                                       .SingleOrDefault(),
            InitiateLoginUri = client.InitiateLoginUri,
            PostLogoutRedirectUri = client.PostLogoutRedirectUris
                                       .Select(x => x.PostLogoutRedirectUri)
                                       .SingleOrDefault(),
            FrontChannelLogoutUri = client.FrontChannelLogoutUri,
            BackChannelLogoutUri = client.BackChannelLogoutUri,
        };
    }

    public async Task CreateAsync(CreateClientModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var client = new Duende.IdentityServer.Models.Client
        {
            ClientId = model.ClientId.Trim(),
            ClientName = model.Name?.Trim(),
            RequireConsent = model.RequireConsent,
            AllowRememberConsent = true,
            AllowedGrantTypes = model.Flow == Flow.ClientCredentials
                                ? GrantTypes.ClientCredentials
                                : GrantTypes.Code
        };

        if (!string.IsNullOrWhiteSpace(model.Secret))
        {
            client.ClientSecrets.Add(new Duende.IdentityServer.Models.Secret(model.Secret.Sha256()));
        }

        if (model.Flow != Flow.ClientCredentials)
        {
            client.AllowOfflineAccess = true;
        }

        _context.Clients.Add(client.ToEntity());
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EditClientModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var client = await _context.Clients
            .Include(x => x.AllowedGrantTypes)
            .Include(x => x.AllowedScopes)
            .Include(x => x.RedirectUris)
            .Include(x => x.PostLogoutRedirectUris)
            .SingleOrDefaultAsync(x => x.ClientId == model.ClientId) ?? throw new ArgumentException("Invalid Client Id");

        // Name / consent...
        client.ClientName = model.Name?.Trim();
        client.RequireConsent = model.RequireConsent;
        client.AllowRememberConsent = true;

        // SCOPES: model.AllowedScopes is now List<string>
        var desired = model.AllowedScopes;
        var current = client.AllowedScopes.Select(x => x.Scope).ToList();
        var toRemove = current.Except(desired).ToList();
        var toAdd = desired.Except(current).ToList();

        if (toRemove.Any())
        {
            client.AllowedScopes.RemoveAll(x => toRemove.Contains(x.Scope));
        }

        if (toAdd.Any())
        {
            client.AllowedScopes.AddRange(toAdd.Select(s => new ClientScope { Scope = s }));
        }

        // REDIRECTS & LOGOUT URIs (unchanged)...
        var flow = client.AllowedGrantTypes.Select(x => x.GrantType).Single() == GrantType.ClientCredentials
                   ? Flow.ClientCredentials
                   : Flow.CodeFlowWithPkce;

        if (flow == Flow.CodeFlowWithPkce)
        {
            // RedirectUri
            var existingRedirect = client.RedirectUris.SingleOrDefault()?.RedirectUri;
            if (existingRedirect != model.RedirectUri)
            {
                client.RedirectUris.Clear();
                if (!string.IsNullOrWhiteSpace(model.RedirectUri))
                {
                    client.RedirectUris.Add(new ClientRedirectUri { RedirectUri = model.RedirectUri.Trim() });
                }
            }

            // InitiateLoginUri
            client.InitiateLoginUri = model.InitiateLoginUri;

            // PostLogoutRedirectUri
            var existingPostLogout = client.PostLogoutRedirectUris.SingleOrDefault()?.PostLogoutRedirectUri;
            if (existingPostLogout != model.PostLogoutRedirectUri)
            {
                client.PostLogoutRedirectUris.Clear();
                if (!string.IsNullOrWhiteSpace(model.PostLogoutRedirectUri))
                {
                    client.PostLogoutRedirectUris.Add(new ClientPostLogoutRedirectUri { PostLogoutRedirectUri = model.PostLogoutRedirectUri.Trim() });
                }
            }

            // FrontChannelLogoutUri
            client.FrontChannelLogoutUri = model.FrontChannelLogoutUri?.Trim();

            // BackChannelLogoutUri
            client.BackChannelLogoutUri = model.BackChannelLogoutUri?.Trim();
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string clientId)
    {
        var client = await _context.Clients.SingleOrDefaultAsync(x => x.ClientId == clientId);

        if (client == null)
        {
            throw new ArgumentException("Invalid Client Id");
        }

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
    }


}

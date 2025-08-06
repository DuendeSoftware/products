using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using IdentityServerTemplate.Extensions;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerTemplate.Pages.Admin.Clients;

public class ClientSummaryModel
{
    [Required]
    [DisplayName("Client Id")]
    public string ClientId { get; set; } = default!;

    [DisplayName("Client Name")]
    public string? Name { get; set; }

    [Required]
    [DisplayName("Flow")]
    public Flow Flow { get; set; }
}

public class CreateClientModel : ClientSummaryModel
{
    [DisplayName("Client Secret")]
    public string? Secret { get; set; }

    [DisplayName("Require Consent")]
    public bool RequireConsent { get; set; } = false;

    [DisplayName("Allow Remember Consent")]
    public bool AllowRememberConsent { get; set; } = false;

    [DisplayName("Application Base URL")]
    public string? BaseUrl { get; set; } = string.Empty;

    [DisplayName("Redirect URI")]
    public string? RedirectUri { get; set; }
}

public class EditClientModel : CreateClientModel, IValidatableObject
{
    [Required]
    [DisplayName("Allowed Scopes")]
    public List<string> AllowedScopes { get; set; } = [];

    [DisplayName("Initiate Login URI")]
    public string? InitiateLoginUri { get; set; }

    [DisplayName("Logo URI")]
    public string? LogoUri { get; set; }

    [DisplayName("Post Logout Redirect URI")]
    public string? PostLogoutRedirectUri { get; set; }

    [DisplayName("Front Channel Logout URI")]
    public string? FrontChannelLogoutUri { get; set; }

    [DisplayName("Back Channel Logout URI")]
    public string? BackChannelLogoutUri { get; set; }

    [DisplayName("Access Token Lifetime in Seconds")]
    public int AccessTokenLifetime { get; set; } = 3600;

    private static readonly string[] memberNames = ["RedirectUri"];

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

    public string? ClientConfigurationSample { get; set; }
}

public enum Flow
{
    ClientCredentials,
    CodeFlowWithPkce
}

public class ClientRepository(ConfigurationDbContext context)
{
    public async Task<IEnumerable<ClientSummaryModel>> GetAllAsync(string? filter = null)
    {
        var grants = new[] { GrantType.AuthorizationCode, GrantType.ClientCredentials };

        var query = context.Clients
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
        var client = await context.Clients
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
            AllowRememberConsent = client.AllowRememberConsent,
            RequireConsent = client.RequireConsent,
            LogoUri = client.LogoUri,
            AccessTokenLifetime = client.AccessTokenLifetime > 0
                ? client.AccessTokenLifetime
                : 3600, // Default to 1 hour if not set
        };
    }

    public async Task SetClientSecret(string clientId, string secret, string? description)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(secret);

        var client = await context.Clients
            .Include(x => x.ClientSecrets)
            .SingleOrDefaultAsync(x => x.ClientId == clientId) ?? throw new ArgumentException("Invalid Client Id");

        client.ClientSecrets.Clear();
        client.ClientSecrets.Add(new ClientSecret { Value = secret.Sha256(), Description = description });

        await context.SaveChangesAsync();
    }

    public async Task ClearClientSecret(string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        var client = await context.Clients
            .Include(x => x.ClientSecrets)
            .SingleOrDefaultAsync(x => x.ClientId == clientId) ?? throw new ArgumentException("Invalid Client Id");

        client.ClientSecrets.Clear();
        await context.SaveChangesAsync();
    }

    public async Task CreateAsync(CreateClientModel model)
    {
        var defaultInteractiveScopes = new[] { "openid" };
        var defaultMachineToMachineScopes = Array.Empty<string>();

        ArgumentNullException.ThrowIfNull(model);

        var exists = await context.Clients.AnyAsync(x => x.ClientId == model.Name);
        if (exists)
        {
            throw new ValidationException($"A Client with the id '{model.ClientId}' already exists.");
        }

        var client = new Duende.IdentityServer.Models.Client
        {
            ClientId = model.ClientId.Trim(),
            ClientName = model.Name?.Trim(),
            RequireConsent = model.RequireConsent,
            AllowRememberConsent = model.AllowRememberConsent,
            AccessTokenLifetime = 3600,

            AllowedGrantTypes = model.Flow == Flow.ClientCredentials
                                ? GrantTypes.ClientCredentials
                                : GrantTypes.Code,

            AllowedScopes = model.Flow == Flow.CodeFlowWithPkce ? defaultInteractiveScopes : defaultMachineToMachineScopes
        };

        if (!string.IsNullOrWhiteSpace(model.Secret))
        {
            client.ClientSecrets.Add(new Duende.IdentityServer.Models.Secret(model.Secret.Sha256()));
        }

        if (model.Flow != Flow.ClientCredentials)
        {
            client.AllowOfflineAccess = true;
        }

        // Infer URLs based on the flow and redirect URIs
        client.InferInteractiveClientURLs(
            model.BaseUrl?.Trim() ?? string.Empty,
            redirectPath: "signin-oidc",
            initiateLoginPath: "account/login",
            postLogoutRedirectPath: "signout-callback-oidc",
            frontChannelLogoutPath: "signout-oidc"
        );

        context.Clients.Add(client.ToEntity());
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EditClientModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var client = await context.Clients
            .Include(x => x.AllowedGrantTypes)
            .Include(x => x.AllowedScopes)
            .Include(x => x.RedirectUris)
            .Include(x => x.PostLogoutRedirectUris)
            .SingleOrDefaultAsync(x => x.ClientId == model.ClientId) ?? throw new ArgumentException("Invalid Client Id");

        // Name, consent, logo etc...
        client.ClientName = model.Name?.Trim();
        client.RequireConsent = model.RequireConsent;
        client.AllowRememberConsent = model.AllowRememberConsent;
        client.LogoUri = model.LogoUri?.Trim();

        client.AccessTokenLifetime = model.AccessTokenLifetime > 0
            ? model.AccessTokenLifetime
            : 3600; // Default to 1 hour if not set

        // SCOPES: model.AllowedScopes is now List<string>
        var desired = model.AllowedScopes;
        var current = client.AllowedScopes.Select(x => x.Scope).ToList();
        var toRemove = current.Except(desired).ToList();
        var toAdd = desired.Except(current).ToList();

        if (toRemove.Count != 0)
        {
            client.AllowedScopes.RemoveAll(x => toRemove.Contains(x.Scope));
        }

        if (toAdd.Count != 0)
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

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string clientId)
    {
        var client = await context.Clients.SingleOrDefaultAsync(x => x.ClientId == clientId)
            ?? throw new ArgumentException("Invalid Client Id");

        context.Clients.Remove(client);
        await context.SaveChangesAsync();
    }

}

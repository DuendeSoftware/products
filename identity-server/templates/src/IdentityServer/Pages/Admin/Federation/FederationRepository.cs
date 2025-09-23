using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public class ProviderSummaryModel
{
    [Required]
    [DisplayName("Type")]
    public string Type { get; set; } = default!;

    [Required]
    [DisplayName("Scheme")]
    public string Scheme { get; set; } = default!;

    [DisplayName("Display Name")]
    public string? Name { get; set; }

    [DisplayName("Icon URL")]
    public string? IconUrl { get; set; }

    [DisplayName("Enabled")]
    public bool Enabled { get; set; } = true;

    public string ToFriendlyType() =>
        Type switch
        {
            "oidc" => "OpenID Connect",
            _ => Type
        };
}

public class CreateProviderModel : ProviderSummaryModel
{
    [Required]
    [DisplayName("Authority URL")]
    public string Authority { get; set; } = string.Empty;

    [Required]
    [DisplayName("Client ID")]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [DisplayName("Client Secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    [DisplayName("Response type")]
    public string ResponseType { get; set; } = "code";

    [Required]
    [DisplayName("Scope")]
    public string Scope { get; set; } = "openid profile";
}

public class EditProviderModel : CreateProviderModel, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = new List<ValidationResult>();
        return errors;
    }

    [DisplayName("Callback URL")]
    public string? CallbackUrl { get; set; }
}

public class FederationRepository(ConfigurationDbContext context)
{
    public async Task<IEnumerable<ProviderSummaryModel>> GetAllAsync(string? filter = null)
    {
        var query = context.IdentityProviders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.Scheme.Contains(filter) || x.DisplayName.Contains(filter));
        }

        var result = query
            .Select(x => x.ToModel())
            .Select(x => new ProviderSummaryModel
            {
                Type = x.Type,
                Scheme = x.Scheme,
                Name = x.DisplayName,
                IconUrl = x.Properties["IconUrl"],
                Enabled = x.Enabled
            });

        return await result.ToArrayAsync();
    }

    public async Task<EditProviderModel?> GetBySchemeAsync(string scheme)
    {
        var identityProvider = await context.IdentityProviders
            .Where(x => x.Scheme == scheme)
            .SingleOrDefaultAsync();

        if (identityProvider == null)
        {
            return null;
        }

        var identityProviderModel = identityProvider.ToModel();

        var model = new EditProviderModel
        {
            Type = identityProviderModel.Type,
            Scheme = identityProviderModel.Scheme,
            Name = identityProviderModel.DisplayName,
            IconUrl = identityProviderModel.Properties["IconUrl"],
            Enabled = identityProviderModel.Enabled,
            CallbackUrl = null
        };

        if (string.Equals(model.Type, "oidc", StringComparison.OrdinalIgnoreCase))
        {
            model.Authority = identityProviderModel.Properties["Authority"];
            model.ClientId = identityProviderModel.Properties["ClientId"];
            model.ClientSecret = identityProviderModel.Properties["ClientSecret"];
            model.ResponseType = identityProviderModel.Properties["ResponseType"];
            model.Scope = identityProviderModel.Properties["Scope"];
        }

        return model;
    }

    public async Task CreateAsync(CreateProviderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var exists = await context.IdentityProviders.AnyAsync(x => x.Scheme == model.Scheme);
        if (exists)
        {
            throw new ValidationException($"A provider with the scheme '{model.Scheme}' already exists.");
        }

        var identityProviderModel = new Duende.IdentityServer.Models.IdentityProvider(model.Type)
        {
            Scheme = model.Scheme.Trim(),
            DisplayName = model.Name?.Trim(),
            Enabled = model.Enabled
        };

        identityProviderModel.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;

        if (model.Type == "oidc")
        {
            identityProviderModel.Properties["Authority"] = model.Authority.Trim();
            identityProviderModel.Properties["ClientId"] = model.ClientId.Trim();
            identityProviderModel.Properties["ClientSecret"] = model.ClientSecret.Trim();
            identityProviderModel.Properties["ResponseType"] = model.ResponseType.Trim();
            identityProviderModel.Properties["Scope"] = model.Scope.Trim();
        }

        context.IdentityProviders.Add(identityProviderModel.ToEntity());
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EditProviderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var identityProvider = await context.IdentityProviders
            .SingleOrDefaultAsync(x => x.Scheme == model.Scheme) ?? throw new ArgumentException("Invalid scheme name");

        var identityProviderModel = identityProvider.ToModel();

        identityProviderModel.DisplayName = model.Name?.Trim();
        identityProviderModel.Enabled = model.Enabled;
        identityProviderModel.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;

        if (model.Type == "oidc")
        {
            identityProviderModel.Properties["Authority"] = model.Authority.Trim();
            identityProviderModel.Properties["ClientId"] = model.ClientId.Trim();
            identityProviderModel.Properties["ClientSecret"] = model.ClientSecret.Trim();
            identityProviderModel.Properties["ResponseType"] = model.ResponseType.Trim();
            identityProviderModel.Properties["Scope"] = model.Scope.Trim();
        }

        // Convert back to entity
        var updatedIdentityProvider = identityProviderModel.ToEntity();
        identityProvider.DisplayName = updatedIdentityProvider.DisplayName;
        identityProvider.Enabled = updatedIdentityProvider.Enabled;
        identityProvider.Properties = updatedIdentityProvider.Properties;

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string scheme)
    {
        var identityProvider = await context.IdentityProviders.SingleOrDefaultAsync(x => x.Scheme == scheme)
            ?? throw new ArgumentException("Invalid scheme");

        context.IdentityProviders.Remove(identityProvider);
        await context.SaveChangesAsync();
    }
}

using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using IdentityServerTemplate.Pages.Admin.Federation.Extensions;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public class ProviderSummaryModel
{
    [Required]
    [Display(Name = "Type")]
    public string Type { get; set; } = default!;

    [Required]
    [Display(Name = "Scheme")]
    public string Scheme { get; set; } = default!;

    [Display(Name = "Display name")]
    public string? Name { get; set; }

    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    public IProviderConfigurationModel Configuration { get; set; }
}

public class CreateProviderModel : ProviderSummaryModel
{
}

public class EditProviderModel : CreateProviderModel
{
    [Display(Name = "Callback URL")]
    public string? CallbackUrl { get; set; }
}

public class FederationRepository(
    IEnumerable<IProviderConfigurationModelFactory> providerConfigurationModelFactories,
    ConfigurationDbContext context)
{
    public IProviderConfigurationModelFactory FindProviderConfigurationModelFactoryFor(string type)
    {
        return providerConfigurationModelFactories.FirstOrDefault(x => x.SupportsType(type))
            ?? throw new ArgumentException($"No provider configuration model factory for type '{type}'");
    }

    public IEnumerable<ProviderConfigurationInfo> GetAllProviderConfigurationInfo()
    {
        return providerConfigurationModelFactories.Select(x => x.GetProviderConfigurationInfo()).ToArray();
    }

    public async Task<IEnumerable<ProviderSummaryModel>> GetAllAsync(string? filter = null)
    {
        var query = context.IdentityProviders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.Scheme.Contains(filter) || x.DisplayName.Contains(filter));
        }

        var result = await query
            .Select(x => x.ToModel())
            .ToArrayAsync();

        return result
            .Select(x => new ProviderSummaryModel
            {
                Type = x.Type,
                Scheme = x.Scheme,
                Name = x.DisplayName,
                Enabled = x.Enabled,
                Configuration = FindProviderConfigurationModelFactoryFor(x.Type).CreateFrom(x)
            })
            .ToArray();
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
            Enabled = identityProviderModel.Enabled,
            Configuration = FindProviderConfigurationModelFactoryFor(identityProviderModel.Type).CreateFrom(identityProviderModel)
        };

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

        var identityProviderModel = FindProviderConfigurationModelFactoryFor(type: model.Type)
            .UpdateModelFrom(
                identityProviderModel: new Duende.IdentityServer.Models.IdentityProvider(model.Type)
                {
                    Scheme = model.Scheme.Trim(),
                    DisplayName = model.Name?.Trim(),
                    Enabled = model.Enabled
                },
                modelConfiguration: model.Configuration);

        context.IdentityProviders.Add(identityProviderModel.ToEntity());
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EditProviderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var identityProvider = await context.IdentityProviders
            .SingleOrDefaultAsync(x => x.Scheme == model.Scheme) ?? throw new ArgumentException("Invalid scheme name");

        var identityProviderModel = FindProviderConfigurationModelFactoryFor(type: model.Type)
            .UpdateModelFrom(
                identityProviderModel: identityProvider.ToModel(),
                modelConfiguration: model.Configuration);

        // Convert back to entity
        identityProvider.DisplayName = model.Name?.Trim();
        identityProvider.Enabled = model.Enabled;
        identityProvider.Properties = identityProviderModel.ToEntity().Properties;

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

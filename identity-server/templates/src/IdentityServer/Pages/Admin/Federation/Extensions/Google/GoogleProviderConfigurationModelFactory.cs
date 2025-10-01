using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.Google;

public class GoogleProviderConfigurationModelFactory : IProviderConfigurationModelFactory
{
    public ProviderConfigurationInfo GetProviderConfigurationInfo() => new()
    {
        Type = GoogleProviderConfigurationModel.Type,
        Name = GoogleProviderConfigurationModel.Name
    };

    public bool SupportsType(string type) => type == GoogleProviderConfigurationModel.Type;

    public IProviderConfigurationModel Create() => new GoogleProviderConfigurationModel();

    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider)
    {
        var model = new GoogleProviderConfigurationModel();

        model.IconUrl = identityProvider.Properties["IconUrl"];
        model.ClientId = identityProvider.Properties["ClientId"];
        model.ClientSecret = identityProvider.Properties["ClientSecret"];

        return model;
    }

    public void UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var model = (GoogleProviderConfigurationModel)modelConfiguration;

        identityProviderModel.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        identityProviderModel.Properties["ClientId"] = model.ClientId.Trim();
        identityProviderModel.Properties["ClientSecret"] = model.ClientSecret.Trim();
    }
}

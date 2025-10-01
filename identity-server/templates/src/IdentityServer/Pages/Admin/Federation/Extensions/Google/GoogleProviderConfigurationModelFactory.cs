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
        var provider = new GoogleIdentityProvider(identityProvider);

        return new GoogleProviderConfigurationModel
        {
            IconUrl = provider.Properties["IconUrl"],
            ClientId = provider.ClientId ?? "",
            ClientSecret = provider.ClientSecret ?? ""
        };
    }

    public IdentityProvider UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var provider = new GoogleIdentityProvider(identityProviderModel);
        var model = (GoogleProviderConfigurationModel)modelConfiguration;

        provider.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        provider.ClientId = model.ClientId.Trim();
        provider.ClientSecret = model.ClientSecret.Trim();

        return provider;
    }
}

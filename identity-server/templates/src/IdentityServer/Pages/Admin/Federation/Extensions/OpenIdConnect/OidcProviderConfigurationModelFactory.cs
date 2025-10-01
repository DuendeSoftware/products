using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.OpenIdConnect;

public class OidcProviderConfigurationModelFactory : IProviderConfigurationModelFactory
{
    public ProviderConfigurationInfo GetProviderConfigurationInfo() => new()
    {
        Type = OidcProviderConfigurationModel.Type,
        Name = OidcProviderConfigurationModel.Name
    };

    public bool SupportsType(string type) => type == OidcProviderConfigurationModel.Type;

    public IProviderConfigurationModel Create() => new OidcProviderConfigurationModel();

    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider)
    {
        var provider = new OidcProvider(identityProvider);

        return new OidcProviderConfigurationModel
        {
            IconUrl = provider.Properties["IconUrl"],
            Authority = provider.Authority ?? "",
            ClientId = provider.ClientId ?? "",
            ClientSecret = provider.ClientSecret ?? "",
            UsePkce = provider.UsePkce,
            ResponseType = provider.ResponseType,
            Scope = provider.Scope
        };
    }

    public IdentityProvider UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var provider = new OidcProvider(identityProviderModel);
        var model = (OidcProviderConfigurationModel)modelConfiguration;

        provider.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        provider.Authority = model.Authority.Trim();
        provider.ClientId = model.ClientId.Trim();
        provider.ClientSecret = model.ClientSecret.Trim();
        provider.ResponseType = model.ResponseType.Trim();
        provider.Scope = model.Scope.Trim();
        provider.UsePkce = model.UsePkce;

        return provider;
    }
}

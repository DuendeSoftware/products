using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.SAML2;

public class Saml2ProviderConfigurationModelFactory : IProviderConfigurationModelFactory
{
    public ProviderConfigurationInfo GetProviderConfigurationInfo() => new()
    {
        Type = Saml2ProviderConfigurationModel.Type,
        Name = Saml2ProviderConfigurationModel.Name
    };

    public bool SupportsType(string type) => type == Saml2ProviderConfigurationModel.Type;

    public IProviderConfigurationModel Create() => new Saml2ProviderConfigurationModel();

    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider)
    {
        var provider = new Saml2IdentityProvider(identityProvider);

        return new Saml2ProviderConfigurationModel
        {
            IconUrl = provider.Properties["IconUrl"],
            SPEntityId = provider.SPEntityId ?? "",
            IdpEntityId = provider.IdpEntityId ?? ""
        };
    }

    public IdentityProvider UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var provider = new Saml2IdentityProvider(identityProviderModel);
        var model = (Saml2ProviderConfigurationModel)modelConfiguration;

        provider.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        provider.SPEntityId = model.SPEntityId.Trim();
        provider.IdpEntityId = model.IdpEntityId.Trim();

        return provider;
    }
}
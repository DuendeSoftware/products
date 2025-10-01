using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.WsFederation;

public class WsFederationProviderConfigurationModelFactory : IProviderConfigurationModelFactory
{
    public ProviderConfigurationInfo GetProviderConfigurationInfo() => new()
    {
        Type = WsFederationProviderConfigurationModel.Type,
        Name = WsFederationProviderConfigurationModel.Name
    };

    public bool SupportsType(string type) => type == WsFederationProviderConfigurationModel.Type;

    public IProviderConfigurationModel Create() => new WsFederationProviderConfigurationModel();

    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider)
    {
        var provider = new WsFederationProvider(identityProvider);

        return new WsFederationProviderConfigurationModel
        {
            IconUrl = provider.Properties["IconUrl"],
            MetadataAddress = provider.MetadataAddress ?? "",
            RelyingPartyId = provider.RelyingPartyId ?? "",
            AllowIdpInitiated = provider.AllowIdpInitiated
        };
    }

    public IdentityProvider UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var provider = new WsFederationProvider(identityProviderModel);
        var model = (WsFederationProviderConfigurationModel)modelConfiguration;

        provider.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        provider.MetadataAddress = model.MetadataAddress.Trim();
        provider.RelyingPartyId = model.RelyingPartyId.Trim();
        provider.AllowIdpInitiated = model.AllowIdpInitiated;

        return provider;
    }
}

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
        var model = new WsFederationProviderConfigurationModel();

        model.IconUrl = identityProvider.Properties["IconUrl"];
        model.MetadataAddress = identityProvider.Properties["MetadataAddress"];
        model.RelyingPartyId = identityProvider.Properties["RelyingPartyId"];
        model.AllowIdpInitiated = identityProvider.Properties.ContainsKey("AllowIdpInitiated") &&
                                  "true".Equals(identityProvider.Properties["AllowIdpInitiated"], StringComparison.Ordinal);

        return model;
    }

    public void UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration)
    {
        var model = (WsFederationProviderConfigurationModel)modelConfiguration;

        identityProviderModel.Properties["IconUrl"] = model.IconUrl?.Trim() ?? string.Empty;
        identityProviderModel.Properties["MetadataAddress"] = model.MetadataAddress.Trim();
        identityProviderModel.Properties["RelyingPartyId"] = model.RelyingPartyId.Trim();
        identityProviderModel.Properties["AllowIdpInitiated"] = model.AllowIdpInitiated ? "true" : "false";
    }
}
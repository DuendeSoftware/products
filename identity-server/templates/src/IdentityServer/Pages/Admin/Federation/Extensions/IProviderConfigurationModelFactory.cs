using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions;

public interface IProviderConfigurationModelFactory
{
    ProviderConfigurationInfo GetProviderConfigurationInfo();

    public bool SupportsType(string type);

    public IProviderConfigurationModel Create();
    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider);

    IdentityProvider UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration);
}

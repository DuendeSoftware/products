using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public class ProviderConfigurationInfo
{
    public string Type { get; set; }
    public string Name { get; set; }
}

public interface IProviderConfigurationModel
{
    string ToFriendlyType();

    bool IsIconUrlEditable();

    string? IconUrl { get; set; }
}

public interface IProviderConfigurationModelFactory
{
    ProviderConfigurationInfo GetProviderConfigurationInfo();

    public bool SupportsType(string type);

    public IProviderConfigurationModel Create();
    public IProviderConfigurationModel CreateFrom(IdentityProvider identityProvider);

    void UpdateModelFrom(IdentityProvider identityProviderModel, IProviderConfigurationModel modelConfiguration);
}

using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.Google;

public class GoogleIdentityProvider : IdentityProvider
{
    public GoogleIdentityProvider() : base(GoogleProviderConfigurationModel.Type)
    {
    }

    public GoogleIdentityProvider(IdentityProvider other) : base(GoogleProviderConfigurationModel.Type, other)
    {
    }

    public string? ClientId
    {
        get => this["ClientId"];
        set => this["ClientId"] = value;
    }

    public string? ClientSecret
    {
        get => this["ClientSecret"];
        set => this["ClientSecret"] = value;
    }
}

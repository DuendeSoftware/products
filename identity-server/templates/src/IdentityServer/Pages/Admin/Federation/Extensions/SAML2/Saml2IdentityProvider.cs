using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.SAML2;

public class Saml2IdentityProvider : IdentityProvider
{
    public Saml2IdentityProvider() : base(Saml2ProviderConfigurationModel.Type)
    {
    }

    public Saml2IdentityProvider(IdentityProvider other) : base(Saml2ProviderConfigurationModel.Type, other)
    {
    }

    public string SPEntityId
    {
        get => this["SPEntityId"];
        set => this["SPEntityId"] = value;
    }

    public string IdpEntityId
    {
        get => this["IdpEntityId"];
        set => this["IdpEntityId"] = value;
    }
}
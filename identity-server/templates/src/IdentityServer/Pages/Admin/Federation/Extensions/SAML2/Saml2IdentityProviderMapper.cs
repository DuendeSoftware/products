using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.SAML2;

public class Saml2IdentityProviderMapper : IFederationIdentityProviderMapper
{
    public IdentityProvider? MapIdp(Duende.IdentityServer.EntityFramework.Entities.IdentityProvider idp)
    {
        if (idp.Type == Saml2ProviderConfigurationModel.Type)
        {
            return new Saml2IdentityProvider(idp.ToModel());
        }

        return null;
    }
}
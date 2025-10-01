using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.WsFederation;

public class WsFederationProviderMapper : IFederationIdentityProviderMapper
{
    public IdentityProvider? MapIdp(Duende.IdentityServer.EntityFramework.Entities.IdentityProvider idp)
    {
        if (idp.Type == WsFederationProviderConfigurationModel.Type)
        {
            return new WsFederationProvider(idp.ToModel());
        }

        return null;
    }
}
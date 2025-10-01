using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.Google;

public class GoogleIdentityProviderMapper : IFederationIdentityProviderMapper
{
    public IdentityProvider? MapIdp(Duende.IdentityServer.EntityFramework.Entities.IdentityProvider idp)
    {
        if (idp.Type == GoogleProviderConfigurationModel.Type)
        {
            return new GoogleIdentityProvider(idp.ToModel());
        }

        return null;
    }
}
using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions;

public interface IFederationIdentityProviderMapper
{
    IdentityProvider? MapIdp(Duende.IdentityServer.EntityFramework.Entities.IdentityProvider idp);
}
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions;

public class FederationIdentityProviderStore(
    IConfigurationDbContext context,
    IEnumerable<IFederationIdentityProviderMapper> mappers,
    ILogger<IdentityProviderStore> logger,
    ICancellationTokenProvider cancellationTokenProvider)
    : IdentityProviderStore(context, logger, cancellationTokenProvider)
{
    protected override IdentityProvider? MapIdp(Duende.IdentityServer.EntityFramework.Entities.IdentityProvider idp)
    {
        var model = base.MapIdp(idp);
        if (model != null)
        {
            return model;
        }

        foreach (var mapper in mappers)
        {
            model = mapper.MapIdp(idp);
            if (model != null)
            {
                return model;
            }
        }

        return null;
    }
}

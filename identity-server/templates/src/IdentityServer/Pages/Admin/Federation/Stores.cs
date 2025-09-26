using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace IdentityServerTemplate.Pages.Admin.Federation;

public class FederationIdentityProviderStore : IdentityProviderStore
{
    public FederationIdentityProviderStore(IConfigurationDbContext context, ILogger<IdentityProviderStore> logger, ICancellationTokenProvider cancellationTokenProvider)
        : base(context, logger, cancellationTokenProvider)
    {
    }

    protected override IdentityProvider MapIdp(Duende.IdentityServer.EntityFramework.Entities.IdentityProvider idp) => base.MapIdp(idp) ?? idp.ToModel();
}

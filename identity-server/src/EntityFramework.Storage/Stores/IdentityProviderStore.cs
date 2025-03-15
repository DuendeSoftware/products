// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of IClientStore thats uses EF.
/// </summary>
/// <seealso cref="IClientStore" />
public class IdentityProviderStore : IIdentityProviderStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IConfigurationDbContext Context;

    /// <summary>
    /// The CancellationToken provider.
    /// </summary>
    protected readonly ICancellationTokenProvider CancellationTokenProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<IdentityProviderStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityProviderStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationTokenProvider"></param>
    /// <exception cref="ArgumentNullException">context</exception>
    public IdentityProviderStore(IConfigurationDbContext context, ILogger<IdentityProviderStore> logger, ICancellationTokenProvider cancellationTokenProvider)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        CancellationTokenProvider = cancellationTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IdentityProviderName>> GetAllSchemeNamesAsync()
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("IdentityProviderStore.GetAllSchemeNames");

        var query = Context.IdentityProviders.Select(x => new IdentityProviderName
        {
            Enabled = x.Enabled,
            Scheme = x.Scheme,
            DisplayName = x.DisplayName
        });

        return await query.ToArrayAsync(CancellationTokenProvider.CancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IdentityProvider> GetBySchemeAsync(string scheme)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("IdentityProviderStore.GetByScheme");
        activity?.SetTag(Tracing.Properties.Scheme, scheme);

        var idp = (await Context.IdentityProviders.AsNoTracking().Where(x => x.Scheme == scheme)
                .ToArrayAsync(CancellationTokenProvider.CancellationToken))
            .SingleOrDefault(x => x.Scheme == scheme);
        if (idp == null)
        {
            return null;
        }

        var result = MapIdp(idp);
        if (result == null)
        {
            Logger.LogError("Identity provider record found in database, but mapping failed for scheme {scheme} and protocol type {protocol}", idp.Scheme, idp.Type);
        }

        return result;
    }

    /// <summary>
    /// Maps from the identity provider entity to identity provider model.
    /// </summary>
    /// <param name="idp"></param>
    /// <returns></returns>
    protected virtual IdentityProvider MapIdp(Entities.IdentityProvider idp)
    {
        if (idp.Type == "oidc")
        {
            return new OidcProvider(idp.ToModel());
        }

        return null;
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable


using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of IIdentityProviderStore that uses EF.
/// </summary>
/// <seealso cref="IIdentityProviderStore" />
public class IdentityProviderStore : IIdentityProviderStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IConfigurationDbContext Context;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<IdentityProviderStore> Logger;

    /// <summary>
    /// The identity provider factory used to construct derived types.
    /// </summary>
    protected readonly IIdentityProviderFactory IdentityProviderFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityProviderStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="identityProviderFactory">The factory for constructing derived identity provider types.</param>
    /// <exception cref="ArgumentNullException">context</exception>
    public IdentityProviderStore(
        IConfigurationDbContext context,
        ILogger<IdentityProviderStore> logger,
        IIdentityProviderFactory identityProviderFactory)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        IdentityProviderFactory = identityProviderFactory ?? throw new ArgumentNullException(nameof(identityProviderFactory));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("IdentityProviderStore.GetAllSchemeNames");

        var query = Context.IdentityProviders.Select(x => new IdentityProviderName
        {
            Enabled = x.Enabled,
            Scheme = x.Scheme,
            DisplayName = x.DisplayName
        });

        return await query.ToArrayAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IdentityProvider?> GetBySchemeAsync(string scheme, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("IdentityProviderStore.GetByScheme");
        activity?.SetTag(Tracing.Properties.Scheme, scheme);

        var idp = (await Context.IdentityProviders.AsNoTracking().Where(x => x.Scheme == scheme)
                .ToArrayAsync(ct))
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
    /// Uses the <see cref="IIdentityProviderFactory"/> to construct the appropriate derived type
    /// based on the provider's <see cref="IdentityProvider.Type"/>.
    /// </summary>
    /// <param name="idp"></param>
    /// <returns></returns>
    protected virtual IdentityProvider? MapIdp(Entities.IdentityProvider idp)
    {
        var baseModel = idp.ToModel();
        return IdentityProviderFactory.Create(baseModel);
    }
}

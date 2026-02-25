// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Runtime.CompilerServices;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of ISamlServiceProviderStore that uses EF.
/// </summary>
public class SamlServiceProviderStore : ISamlServiceProviderStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IConfigurationDbContext Context;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<SamlServiceProviderStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamlServiceProviderStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    public SamlServiceProviderStore(
        IConfigurationDbContext context,
        ILogger<SamlServiceProviderStore> logger)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<Models.SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlServiceProviderStore.FindByEntityId");
        activity?.SetTag(Tracing.Properties.SamlEntityId, entityId);

        var query = Context.SamlServiceProviders
            .Where(x => x.EntityId == entityId)
            .Include(x => x.AssertionConsumerServiceUrls)
            .Include(x => x.SigningCertificates)
            .Include(x => x.EncryptionCertificates)
            .Include(x => x.ClaimMappings)
            .AsNoTracking()
            .AsSplitQuery();

        var entity = (await query.ToArrayAsync(ct))
            .SingleOrDefault(x => x.EntityId == entityId);
        if (entity == null)
        {
            return null;
        }

        var model = entity.ToModel();
        Logger.LogDebug("{entityId} found in database: {found}", entityId, model != null);
        return model;
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<Models.SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlServiceProviderStore.GetAllSamlServiceProviders");

        var query = Context.SamlServiceProviders
            .Include(x => x.AssertionConsumerServiceUrls)
            .Include(x => x.SigningCertificates)
            .Include(x => x.EncryptionCertificates)
            .Include(x => x.ClaimMappings)
            .AsNoTracking()
            .AsSplitQuery();

        var count = 0;
        await foreach (var entity in query.AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            count++;
            yield return entity.ToModel();
        }

        Logger.LogDebug("Retrieved {count} SAML service providers for enumeration", count);
    }
}

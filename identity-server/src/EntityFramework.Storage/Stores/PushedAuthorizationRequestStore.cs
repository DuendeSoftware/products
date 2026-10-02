// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <inheritdoc />
public class PushedAuthorizationRequestStore : IPushedAuthorizationRequestStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IPersistedGrantDbContext Context;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushedAuthorizationRequestStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    public PushedAuthorizationRequestStore(IPersistedGrantDbContext context, ILogger<PushedAuthorizationRequestStore> logger)
    {
        Context = context;
        Logger = logger;
    }

    /// <inheritdoc />
    public async Task ConsumeByHashAsync(string referenceValueHash, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PersistedGrantStore.Remove");
        Log.RemovingValuePushedAuthorizationFromDatabase(Logger, referenceValueHash);
        var numDeleted = await Context.PushedAuthorizationRequests
            .Where(par => par.ReferenceValueHash == referenceValueHash)
            .ExecuteDeleteAsync(ct);
        if (numDeleted != 1)
        {
            Log.AttemptedToRemoveValuePushedAuthorizationRequestBecause(Logger, referenceValueHash);
        }
    }

    /// <inheritdoc />
    public virtual async Task<Models.PushedAuthorizationRequest> GetByHashAsync(string referenceValueHash, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PushedAuthorizationRequestStore.Get");

        var par = (await Context.PushedAuthorizationRequests
                .AsNoTracking().Where(x => x.ReferenceValueHash == referenceValueHash)
                .ToArrayAsync(ct))
                .SingleOrDefault(x => x.ReferenceValueHash == referenceValueHash);
        var model = par?.ToModel();

        Log.ValuePushedAuthorizationFoundInDatabaseValue(Logger, referenceValueHash, model != null);

        return model;
    }


    /// <inheritdoc />
    public virtual async Task StoreAsync(Models.PushedAuthorizationRequest par, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PushedAuthorizationStore.Store");

        Context.PushedAuthorizationRequests.Add(par.ToEntity());
        try
        {
            await Context.SaveChangesAsync(ct);
        }
        // REVIEW - Is this exception possible, since we don't try to load (and then update) an existing entity?
        // I think it isn't, but what happens if we somehow two calls to StoreAsync with the same PAR are made?
        catch (DbUpdateConcurrencyException ex)
        {
            Log.ExceptionUpdatingValuePushedAuthorizationInDatabaseValue(Logger, par.ReferenceValueHash, ex.Message);
        }
    }
}

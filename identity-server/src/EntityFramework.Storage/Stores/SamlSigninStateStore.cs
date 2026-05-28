// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Entity Framework-backed implementation of <see cref="ISamlSigninStateStore"/>.
/// </summary>
public class SamlSigninStateStore : ISamlSigninStateStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IPersistedGrantDbContext Context;

    /// <summary>
    /// The time provider.
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The serializer for SAML signin state.
    /// </summary>
    protected readonly ISamlSigninStateSerializer Serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamlSigninStateStore"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="serializer">The SAML signin state serializer.</param>
    /// <param name="logger">The logger.</param>
    public SamlSigninStateStore(
        IPersistedGrantDbContext context,
        TimeProvider timeProvider,
        ISamlSigninStateSerializer serializer,
        ILogger<SamlSigninStateStore> logger)
    {
        Context = context;
        TimeProvider = timeProvider;
        Serializer = serializer;
        Logger = logger;
    }

    /// <inheritdoc />
    public virtual async Task<Guid> StoreSigninRequestStateAsync(SamlAuthenticationState state, Ct ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.StoreSigninRequestState");

        if (state.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML signin state.", nameof(state));
        }

        var stateId = Guid.NewGuid();
        var entity = state.ToEntity(stateId, state.ExpiresAtUtc, Serializer);
        Context.SamlSigninStates.Add(entity);

        try
        {
            await Context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogWarning("exception storing SAML signin state {stateId} in database: {error}", stateId, ex.Message);
            throw;
        }

        Logger.LogDebug("stored SAML signin state {stateId} in database", stateId);
        return stateId;
    }

    /// <inheritdoc />
    public virtual async Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(Guid stateId, Ct ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.RetrieveSigninRequestState");

        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var entity = await Context.SamlSigninStates
            .AsNoTracking()
            .Where(x => x.StateId == stateId && x.ExpiresAtUtc > now)
            .SingleOrDefaultAsync(ct);

        var model = entity.ToModel(Serializer);
        Logger.LogDebug("SAML signin state {stateId} found in database: {found}", stateId, model != null);
        return model;
    }

    /// <inheritdoc />
    public virtual async Task RemoveSigninRequestStateAsync(Guid stateId, Ct ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.RemoveSigninRequestState");

        await Context.SamlSigninStates
            .Where(x => x.StateId == stateId)
            .ExecuteDeleteAsync(ct);

        Logger.LogDebug("removed SAML signin state {stateId} from database", stateId);
    }

    /// <inheritdoc />
    public virtual async Task UpdateSigninRequestStateAsync(Guid stateId, SamlAuthenticationState state, Ct ct = default)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.UpdateSigninRequestState");

        var serializedState = Serializer.Serialize(state);
        var now = TimeProvider.GetUtcNow().UtcDateTime;

        var updated = await Context.SamlSigninStates
            .Where(x => x.StateId == stateId && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.SerializedState, serializedState), ct);

        if (updated == 0)
        {
            Logger.LogWarning("SAML signin state {stateId} not found or expired for update", stateId);
        }
        else
        {
            Logger.LogDebug("updated SAML signin state {stateId} in database", stateId);
        }
    }
}

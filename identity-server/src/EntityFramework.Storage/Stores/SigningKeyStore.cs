// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of ISigningKeyStore thats uses EF.
/// </summary>
/// <seealso cref="ISigningKeyStore" />
public class SigningKeyStore : ISigningKeyStore
{
    private const string Use = "signing";

    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IPersistedGrantDbContext Context;

    /// <summary>
    /// The CancellationToken provider.
    /// </summary>
    protected readonly ICancellationTokenProvider CancellationTokenProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<SigningKeyStore> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningKeyStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationTokenProvider"></param>
    /// <exception cref="ArgumentNullException">context</exception>
    public SigningKeyStore(IPersistedGrantDbContext context, ILogger<SigningKeyStore> logger, ICancellationTokenProvider cancellationTokenProvider)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        CancellationTokenProvider = cancellationTokenProvider;
    }

    /// <summary>
    /// Loads all keys from store.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<SerializedKey>> LoadKeysAsync()
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SigningKeyStore.LoadKeys");

        var entities = await Context.Keys.Where(x => x.Use == Use)
            .AsNoTracking()
            .ToArrayAsync(CancellationTokenProvider.CancellationToken);
        return entities.Select(key => new SerializedKey
        {
            Id = key.Id,
            Created = key.Created,
            Version = key.Version,
            Algorithm = key.Algorithm,
            Data = key.Data,
            DataProtected = key.DataProtected,
            IsX509Certificate = key.IsX509Certificate
        });
    }

    /// <summary>
    /// Persists new key in store.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task StoreKeyAsync(SerializedKey key)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SigningKeyStore.StoreKey");

        var entity = new Key
        {
            Id = key.Id,
            Use = Use,
            Created = key.Created,
            Version = key.Version,
            Algorithm = key.Algorithm,
            Data = key.Data,
            DataProtected = key.DataProtected,
            IsX509Certificate = key.IsX509Certificate
        };
        Context.Keys.Add(entity);
        await Context.SaveChangesAsync(CancellationTokenProvider.CancellationToken);
    }

    /// <summary>
    /// Deletes key from storage.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task DeleteKeyAsync(string id)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SigningKeyStore.DeleteKey");

        var item = await Context.Keys.Where(x => x.Use == Use && x.Id == id)
            .FirstOrDefaultAsync(CancellationTokenProvider.CancellationToken);
        if (item != null)
        {
            try
            {
                Context.Keys.Remove(item);
                await Context.SaveChangesAsync(CancellationTokenProvider.CancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entity in ex.Entries)
                {
                    entity.State = EntityState.Detached;
                }

                // already deleted, so we can eat this exception
                Logger.LogDebug("Concurrency exception caught deleting key id {kid}", id);
            }
        }
    }
}

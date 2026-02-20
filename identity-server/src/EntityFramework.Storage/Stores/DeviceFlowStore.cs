// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of IDeviceFlowStore thats uses EF.
/// </summary>
/// <seealso cref="IDeviceFlowStore" />
public class DeviceFlowStore : IDeviceFlowStore
{
    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IPersistedGrantDbContext Context;

    /// <summary>
    ///  The serializer.
    /// </summary>
    protected readonly IPersistentGrantSerializer Serializer;

    /// <summary>
    /// The CancellationToken provider.
    /// </summary>
    protected readonly ICancellationTokenProvider CancellationTokenProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceFlowStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="serializer">The serializer</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationTokenProvider"></param>
    public DeviceFlowStore(
        IPersistedGrantDbContext context,
        IPersistentGrantSerializer serializer,
        ILogger<DeviceFlowStore> logger,
        ICancellationTokenProvider cancellationTokenProvider)
    {
        Context = context;
        Serializer = serializer;
        Logger = logger;
        CancellationTokenProvider = cancellationTokenProvider;
    }

    /// <inheritdoc/>
    public virtual async Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.StoreDeviceAuthorization");

        Context.DeviceFlowCodes.Add(ToEntity(data, deviceCode, userCode));

        await Context.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public virtual async Task<DeviceCode> FindByUserCodeAsync(string userCode, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.FindByUserCode");

        var deviceFlowCodes = (await Context.DeviceFlowCodes.AsNoTracking().Where(x => x.UserCode == userCode)
                .ToArrayAsync(ct))
            .SingleOrDefault(x => x.UserCode == userCode);
        var model = ToModel(deviceFlowCodes?.Data);

        Logger.LogDebug("{userCode} found in database: {userCodeFound}", userCode, model != null);

        return model;
    }

    /// <inheritdoc/>
    public virtual async Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.FindByDeviceCode");

        var deviceFlowCodes = (await Context.DeviceFlowCodes.AsNoTracking().Where(x => x.DeviceCode == deviceCode)
                .ToArrayAsync(ct))
            .SingleOrDefault(x => x.DeviceCode == deviceCode);
        var model = ToModel(deviceFlowCodes?.Data);

        Logger.LogDebug("{deviceCode} found in database: {deviceCodeFound}", deviceCode, model != null);

        return model;
    }

    /// <inheritdoc/>
    public virtual async Task UpdateByUserCodeAsync(string userCode, DeviceCode data, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.UpdateByUserCode");

        var existing = (await Context.DeviceFlowCodes.Where(x => x.UserCode == userCode)
                .ToArrayAsync(ct))
            .SingleOrDefault(x => x.UserCode == userCode);
        if (existing == null)
        {
            Logger.LogError("{userCode} not found in database", userCode);
            throw new InvalidOperationException("Could not update device code");
        }

        Logger.LogDebug("{userCode} found in database", userCode);

        existing.SubjectId = data.Subject?.FindFirst(JwtClaimTypes.Subject).Value;
        existing.SessionId = data.SessionId;
        existing.Description = data.Description;
        existing.Data = Serializer.Serialize(data);

        try
        {
            await Context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogWarning("exception updating {userCode} user code in database: {error}", userCode, ex.Message);
        }
    }

    /// <inheritdoc/>
    public virtual async Task RemoveByDeviceCodeAsync(string deviceCode, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.RemoveByDeviceCode");

        var deviceFlowCodes = (await Context.DeviceFlowCodes.Where(x => x.DeviceCode == deviceCode)
                .ToArrayAsync(ct))
            .SingleOrDefault(x => x.DeviceCode == deviceCode);

        if (deviceFlowCodes != null)
        {
            Logger.LogDebug("removing {deviceCode} device code from database", deviceCode);

            Context.DeviceFlowCodes.Remove(deviceFlowCodes);

            try
            {
                await Context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Logger.LogInformation("exception removing {deviceCode} device code from database: {error}", deviceCode, ex.Message);
            }
        }
        else
        {
            Logger.LogDebug("no {deviceCode} device code found in database", deviceCode);
        }
    }

    /// <summary>
    /// Converts a model to an entity.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="deviceCode"></param>
    /// <param name="userCode"></param>
    /// <returns></returns>
    protected DeviceFlowCodes ToEntity(DeviceCode model, string deviceCode, string userCode)
    {
        if (model == null || deviceCode == null || userCode == null)
        {
            return null;
        }

        return new DeviceFlowCodes
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            ClientId = model.ClientId,
            SubjectId = model.Subject?.FindFirst(JwtClaimTypes.Subject).Value,
            SessionId = model.SessionId,
            Description = model.Description,
            CreationTime = model.CreationTime,
            Expiration = model.CreationTime.AddSeconds(model.Lifetime),
            Data = Serializer.Serialize(model)
        };
    }

    /// <summary>
    /// Converts a serialized DeviceCode to a model.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    protected DeviceCode ToModel(string entity)
    {
        if (entity == null)
        {
            return null;
        }

        return Serializer.Deserialize<DeviceCode>(entity);
    }
}

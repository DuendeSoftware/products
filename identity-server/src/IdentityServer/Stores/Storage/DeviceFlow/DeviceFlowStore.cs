// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Serialization;
using Duende.Storage;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.DeviceFlow;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class DeviceFlowStore(
    DeviceFlowRepository repository,
    IPersistentGrantSerializer serializer,
    ILogger<DeviceFlowStore> logger) : IDeviceFlowStore
{
    /// <inheritdoc/>
    public async Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.StoreDeviceAuthorization");

        var dso = new DeviceFlowDso.V1
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            Data = serializer.Serialize(data)
        };

        var expiration = Duende.Storage.Internal.Expiration.AtAbsolute(
            new DateTimeOffset(DateTime.SpecifyKind(data.CreationTime, DateTimeKind.Utc))
                .AddSeconds(data.Lifetime));

        var result = await repository.CreateAsync(UuidV7.New(), dso, expiration, ct);

        if (result != CreateResult.Success)
        {
            logger.DeviceAuthorizationStoreFailed(LogLevel.Error, result.ToString());
            throw new InvalidOperationException("Could not store device authorization code");
        }
    }

    /// <inheritdoc/>
    public async Task<DeviceCode?> FindByUserCodeAsync(string userCode, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.FindByUserCode");

        var entry = await repository.TryReadByUserCodeAsync(userCode, ct);
        if (entry is null)
        {
            logger.UserCodeNotFound(LogLevel.Debug, userCode);
            return null;
        }

        return serializer.Deserialize<DeviceCode>(entry.Value.Dso.Data);
    }

    /// <inheritdoc/>
    public async Task<DeviceCode?> FindByDeviceCodeAsync(string deviceCode, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.FindByDeviceCode");

        var entry = await repository.TryReadByDeviceCodeAsync(deviceCode, ct);
        if (entry is null)
        {
            logger.DeviceCodeNotFound(LogLevel.Debug, deviceCode);
            return null;
        }

        return serializer.Deserialize<DeviceCode>(entry.Value.Dso.Data);
    }

    /// <inheritdoc/>
    public async Task UpdateByUserCodeAsync(string userCode, DeviceCode data, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.UpdateByUserCode");

        var entry = await repository.TryReadByUserCodeAsync(userCode, ct);
        if (entry is null)
        {
            logger.UserCodeNotFoundForUpdate(LogLevel.Error, userCode);
            throw new InvalidOperationException("Could not update device code");
        }

        var (dso, id, version) = entry.Value;

        var updatedDso = dso with { Data = serializer.Serialize(data) };

        var result = await repository.UpdateAsync(id, updatedDso, version, ct);

        switch (result)
        {
            case UpdateResult.Success:
                break;
            case UpdateResult.DoesNotExist:
                logger.DeviceCodeDeletedDuringUpdate(LogLevel.Warning, userCode);
                break;
            case UpdateResult.UnexpectedVersion:
                logger.DeviceCodeConcurrencyConflict(LogLevel.Warning, userCode);
                break;
            case UpdateResult.KeyConflict:
                // Unreachable: keys are immutable across updates (DeviceCode/UserCode never change).
                throw new InvalidOperationException("Unexpected key conflict updating device code");
        }
    }

    /// <inheritdoc/>
    public async Task RemoveByDeviceCodeAsync(string deviceCode, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DeviceFlowStore.RemoveByDeviceCode");

        logger.RemovingDeviceCode(LogLevel.Debug, deviceCode);

        await repository.DeleteByDeviceCodeAsync(deviceCode, ct);
    }
}

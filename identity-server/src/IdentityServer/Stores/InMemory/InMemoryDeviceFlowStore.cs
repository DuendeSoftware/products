// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory device flow store
/// </summary>
/// <seealso cref="IdentityServer.Stores.IDeviceFlowStore" />
public class InMemoryDeviceFlowStore : IDeviceFlowStore
{
    private readonly List<InMemoryDeviceAuthorization> _repository = new List<InMemoryDeviceAuthorization>();

    /// <inheritdoc/>
    public Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.StoreDeviceAuthorization");

        lock (_repository)
        {
            _repository.Add(new InMemoryDeviceAuthorization(deviceCode, userCode, data));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DeviceCode> FindByUserCodeAsync(string userCode, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.FindByUserCode");

        DeviceCode foundDeviceCode;

        lock (_repository)
        {
            foundDeviceCode = _repository.FirstOrDefault(x => x.UserCode == userCode)?.Data;
        }

        return Task.FromResult(foundDeviceCode);
    }

    /// <inheritdoc/>
    public Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.FindByDeviceCode");

        DeviceCode foundDeviceCode;

        lock (_repository)
        {
            foundDeviceCode = _repository.FirstOrDefault(x => x.DeviceCode == deviceCode)?.Data;
        }

        return Task.FromResult(foundDeviceCode);
    }

    /// <inheritdoc/>
    public Task UpdateByUserCodeAsync(string userCode, DeviceCode data, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.UpdateByUserCode");

        lock (_repository)
        {
            var foundData = _repository.FirstOrDefault(x => x.UserCode == userCode);

            if (foundData != null)
            {
                foundData.Data = data;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveByDeviceCodeAsync(string deviceCode, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.RemoveByDeviceCode");

        lock (_repository)
        {
            var foundData = _repository.FirstOrDefault(x => x.DeviceCode == deviceCode);

            if (foundData != null)
            {
                _repository.Remove(foundData);
            }
        }


        return Task.CompletedTask;
    }

    private class InMemoryDeviceAuthorization
    {
        public InMemoryDeviceAuthorization(string deviceCode, string userCode, DeviceCode data)
        {
            DeviceCode = deviceCode;
            UserCode = userCode;
            Data = data;
        }

        public string DeviceCode { get; }
        public string UserCode { get; }
        public DeviceCode Data { get; set; }
    }
}

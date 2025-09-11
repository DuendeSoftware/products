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
    private readonly List<InMemoryDeviceAuthorization> _repository = new();

    /// <summary>
    /// Stores the device authorization request.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    /// <param name="userCode">The user code.</param>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    public Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.StoreDeviceAuthorization");

        lock (_repository)
        {
            _repository.Add(new InMemoryDeviceAuthorization(deviceCode, userCode, data));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds device authorization by user code.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    public Task<DeviceCode> FindByUserCodeAsync(string userCode)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.FindByUserCode");

        DeviceCode foundDeviceCode;

        lock (_repository)
        {
            foundDeviceCode = _repository.FirstOrDefault(x => x.UserCode == userCode)?.Data;
        }

        return Task.FromResult(foundDeviceCode);
    }

    /// <summary>
    /// Finds device authorization by device code.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    public Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.FindByDeviceCode");

        DeviceCode foundDeviceCode;

        lock (_repository)
        {
            foundDeviceCode = _repository.FirstOrDefault(x => x.DeviceCode == deviceCode)?.Data;
        }

        return Task.FromResult(foundDeviceCode);
    }

    /// <summary>
    /// Updates device authorization, searching by user code.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    /// <param name="data">The data.</param>
    public Task UpdateByUserCodeAsync(string userCode, DeviceCode data)
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

    /// <summary>
    /// Removes the device authorization, searching by device code.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    /// <returns></returns>
    public Task RemoveByDeviceCodeAsync(string deviceCode)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.RemoveByDeviceCode");

        lock (_repository)
        {
            var foundData = _repository.FirstOrDefault(x => x.DeviceCode == deviceCode);

            if (foundData != null)
            {
                _ = _repository.Remove(foundData);
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

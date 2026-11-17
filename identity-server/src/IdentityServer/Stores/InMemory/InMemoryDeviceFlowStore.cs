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
    private readonly PartitionedState<DeviceFlowState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDeviceFlowStore"/> class.
    /// It holds a single partition and isn't split by space; to partition by space use
    /// <see cref="InMemoryDeviceFlowStore(InMemoryStoreState)"/>.
    /// </summary>
    public InMemoryDeviceFlowStore() : this(new InMemoryStoreState())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDeviceFlowStore"/> class
    /// that partitions its data using the given state source.
    /// </summary>
    /// <param name="state">The state source, which decides how data is partitioned.</param>
    public InMemoryDeviceFlowStore(InMemoryStoreState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = new(state.CurrentSpace);
    }

    /// <inheritdoc/>
    public Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.StoreDeviceAuthorization");

        var repository = _state.Current.Items;

        lock (repository)
        {
            repository.Add(new InMemoryDeviceAuthorization(deviceCode, userCode, data));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DeviceCode> FindByUserCodeAsync(string userCode, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.FindByUserCode");

        DeviceCode foundDeviceCode;

        if (!_state.TryGetCurrent(out var state))
        {
            return Task.FromResult<DeviceCode>(null);
        }

        var repository = state.Items;

        lock (repository)
        {
            foundDeviceCode = repository.FirstOrDefault(x => x.UserCode == userCode)?.Data;
        }

        return Task.FromResult(foundDeviceCode);
    }

    /// <inheritdoc/>
    public Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.FindByDeviceCode");

        DeviceCode foundDeviceCode;

        if (!_state.TryGetCurrent(out var state))
        {
            return Task.FromResult<DeviceCode>(null);
        }

        var repository = state.Items;

        lock (repository)
        {
            foundDeviceCode = repository.FirstOrDefault(x => x.DeviceCode == deviceCode)?.Data;
        }

        return Task.FromResult(foundDeviceCode);
    }

    /// <inheritdoc/>
    public Task UpdateByUserCodeAsync(string userCode, DeviceCode data, Ct _)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryDeviceFlowStore.UpdateByUserCode");

        if (!_state.TryGetCurrent(out var state))
        {
            return Task.CompletedTask;
        }

        var repository = state.Items;

        lock (repository)
        {
            var foundData = repository.FirstOrDefault(x => x.UserCode == userCode);

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

        if (!_state.TryGetCurrent(out var state))
        {
            return Task.CompletedTask;
        }

        var repository = state.Items;

        lock (repository)
        {
            var foundData = repository.FirstOrDefault(x => x.DeviceCode == deviceCode);

            if (foundData != null)
            {
                repository.Remove(foundData);
            }
        }


        return Task.CompletedTask;
    }
}

internal sealed class DeviceFlowState
{
    public List<InMemoryDeviceAuthorization> Items { get; } = [];
}

internal sealed class InMemoryDeviceAuthorization
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

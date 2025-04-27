// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Services.Default;

/// <summary>
/// Default wrapper service for IDeviceFlowStore, handling key hashing
/// </summary>
/// <seealso cref="IDeviceFlowCodeService" />
public class DefaultDeviceFlowCodeService : IDeviceFlowCodeService
{
    private readonly IDeviceFlowStore _store;
    private readonly IHandleGenerationService _handleGenerationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDeviceFlowCodeService"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handleGenerationService">The handle generation service.</param>
    public DefaultDeviceFlowCodeService(IDeviceFlowStore store,
        IHandleGenerationService handleGenerationService)
    {
        _store = store;
        _handleGenerationService = handleGenerationService;
    }

    /// <summary>
    /// Stores the device authorization request.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    /// <param name="data">The data.</param>
    public async Task<string> StoreDeviceAuthorizationAsync(string userCode, DeviceCode data)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultDeviceFlowCodeService.SendLogoutNotifStoreDeviceAuthorization");

        var deviceCode = await _handleGenerationService.GenerateAsync();

        await _store.StoreDeviceAuthorizationAsync(deviceCode.Sha256(), userCode.Sha256(), data);

        return deviceCode;
    }

    /// <summary>
    /// Finds device authorization by user code.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    public Task<DeviceCode> FindByUserCodeAsync(string userCode)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultDeviceFlowCodeService.FindByUserCode");

        return _store.FindByUserCodeAsync(userCode.Sha256());
    }

    /// <summary>
    /// Finds device authorization by device code.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    public Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultDeviceFlowCodeService.FindByDeviceCode");

        return _store.FindByDeviceCodeAsync(deviceCode.Sha256());
    }

    /// <summary>
    /// Updates device authorization, searching by user code.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    /// <param name="data">The data.</param>
    public Task UpdateByUserCodeAsync(string userCode, DeviceCode data)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultDeviceFlowCodeService.UpdateByUserCode");

        return _store.UpdateByUserCodeAsync(userCode.Sha256(), data);
    }

    /// <summary>
    /// Removes the device authorization, searching by device code.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    public Task RemoveByDeviceCodeAsync(string deviceCode)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DefaultDeviceFlowCodeService.RemoveByDeviceCode");

        return _store.RemoveByDeviceCodeAsync(deviceCode.Sha256());
    }
}

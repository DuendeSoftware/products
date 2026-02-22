// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Wrapper service for IDeviceFlowStore.
/// </summary>
public interface IDeviceFlowCodeService
{
    /// <summary>
    /// Stores the device authorization request.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    /// <param name="data">The data.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<string> StoreDeviceAuthorizationAsync(string userCode, DeviceCode data, Ct ct);

    /// <summary>
    /// Finds device authorization by user code.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<DeviceCode?> FindByUserCodeAsync(string userCode, Ct ct);

    /// <summary>
    /// Finds device authorization by device code.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<DeviceCode?> FindByDeviceCodeAsync(string deviceCode, Ct ct);

    /// <summary>
    /// Updates device authorization, searching by user code.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    /// <param name="data">The data.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpdateByUserCodeAsync(string userCode, DeviceCode data, Ct ct);

    /// <summary>
    /// Removes the device authorization, searching by device code.
    /// </summary>
    /// <param name="deviceCode">The device code.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveByDeviceCodeAsync(string deviceCode, Ct ct);
}

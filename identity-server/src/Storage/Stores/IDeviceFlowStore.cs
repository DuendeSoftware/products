// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves authorization grants for the OAuth 2.0 Device Authorization
/// Grant (device flow). In the device flow, a device code is used by the client device
/// to poll for authorization, while a shorter user code is displayed to the user and
/// entered on a secondary device to approve the request. This store manages the
/// lifecycle of those paired codes and the associated authorization data.
/// </summary>
public interface IDeviceFlowStore
{
    /// <summary>
    /// Stores a new device authorization request, associating the device code and user
    /// code with the provided authorization data.
    /// </summary>
    /// <param name="deviceCode">
    /// The device code issued to the client device, used to poll for authorization.
    /// </param>
    /// <param name="userCode">
    /// The short user code displayed to the user and entered on a secondary device to
    /// approve the request.
    /// </param>
    /// <param name="data">The authorization data associated with the request.</param>
    /// <param name="ct">The cancellation token.</param>
    Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data, Ct ct);

    /// <summary>
    /// Finds a device authorization request by its user code. This is used during the
    /// user interaction flow when the user enters the code on a secondary device.
    /// </summary>
    /// <param name="userCode">The user code to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="DeviceCode"/> associated with the specified <paramref name="userCode"/>,
    /// or <see langword="null"/> if no matching request exists.
    /// </returns>
    Task<DeviceCode?> FindByUserCodeAsync(string userCode, Ct ct);

    /// <summary>
    /// Finds a device authorization request by its device code. This is used when the
    /// client device polls the token endpoint to check whether the user has approved
    /// the request.
    /// </summary>
    /// <param name="deviceCode">The device code to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="DeviceCode"/> associated with the specified <paramref name="deviceCode"/>,
    /// or <see langword="null"/> if no matching request exists.
    /// </returns>
    Task<DeviceCode?> FindByDeviceCodeAsync(string deviceCode, Ct ct);

    /// <summary>
    /// Updates the authorization data for an existing device authorization request,
    /// looked up by its user code. This is called after the user approves or denies
    /// the request on the secondary device.
    /// </summary>
    /// <param name="userCode">The user code identifying the request to update.</param>
    /// <param name="data">The updated authorization data to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UpdateByUserCodeAsync(string userCode, DeviceCode data, Ct ct);

    /// <summary>
    /// Removes the device authorization request identified by the specified device code.
    /// This is called after the client device has successfully exchanged the device code
    /// for tokens.
    /// </summary>
    /// <param name="deviceCode">The device code identifying the request to remove.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RemoveByDeviceCodeAsync(string deviceCode, Ct ct);
}

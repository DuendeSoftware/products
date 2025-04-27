// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
///  Provide services be used by the user interface to communicate with IdentityServer.
/// </summary>
public interface IDeviceFlowInteractionService
{
    /// <summary>
    /// Gets the authorization context asynchronous.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    Task<DeviceFlowAuthorizationRequest?> GetAuthorizationContextAsync(string userCode);

    /// <summary>
    /// Handles the request asynchronous.
    /// </summary>
    /// <param name="userCode">The user code.</param>
    /// <param name="consent">The consent.</param>
    Task<DeviceFlowInteractionResult> HandleRequestAsync(string userCode, ConsentResponse consent);
}

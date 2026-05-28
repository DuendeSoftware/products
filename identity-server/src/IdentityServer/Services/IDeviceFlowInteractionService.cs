// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Provides services used by the user interface to communicate with IdentityServer during
/// OAuth 2.0 Device Authorization Grant (device flow) authorization.
/// This service is available from the dependency injection system and is typically injected
/// as a constructor parameter into MVC controllers that implement the device flow UI.
/// </summary>
public interface IDeviceFlowInteractionService
{
    /// <summary>
    /// Returns the <see cref="DeviceFlowAuthorizationRequest"/> based on the <paramref name="userCode"/>
    /// entered by the user on the device authorization page.
    /// Use this to retrieve the client and requested scopes so the UI can render the consent page.
    /// </summary>
    /// <param name="userCode">The user code entered by the user on the device authorization page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="DeviceFlowAuthorizationRequest"/> describing the pending device authorization,
    /// including the client identifier and the requested scopes,
    /// or <c>null</c> if the user code is not valid or has expired.
    /// </returns>
    Task<DeviceFlowAuthorizationRequest?> GetAuthorizationContextAsync(string userCode, Ct ct);

    /// <summary>
    /// Completes device authorization for the given <paramref name="userCode"/> by recording
    /// the user's consent decision. Call this after the user has approved or denied the request
    /// on the device consent page.
    /// </summary>
    /// <param name="userCode">The user code identifying the device authorization request to complete.</param>
    /// <param name="consent">The user's consent response, including the scopes approved and whether to remember the decision.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="DeviceFlowInteractionResult"/> indicating whether the authorization succeeded.
    /// Check <see cref="DeviceFlowInteractionResult.IsError"/> and <see cref="DeviceFlowInteractionResult.ErrorDescription"/>
    /// for failure details.
    /// </returns>
    Task<DeviceFlowInteractionResult> HandleRequestAsync(string userCode, ConsentResponse consent, Ct ct);
}

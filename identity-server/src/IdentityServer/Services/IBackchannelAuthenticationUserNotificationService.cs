// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Used to contact users when a Client-Initiated Backchannel Authentication (CIBA) login request has been made.
/// To use CIBA, you must implement this interface and register it in the ASP.NET Core service provider.
/// The implementation is responsible for delivering the login notification to the user via an
/// out-of-band channel such as push notification, SMS, or email.
/// </summary>
public interface IBackchannelAuthenticationUserNotificationService
{
    /// <summary>
    /// Sends a notification to the user prompting them to approve or deny the CIBA login request.
    /// The notification should direct the user to the IdentityServer UI where they can review
    /// and complete the pending <see cref="BackchannelUserLoginRequest"/>.
    /// </summary>
    /// <param name="request">
    /// The backchannel login request containing the details of the authentication request,
    /// including the requesting client and the user to be notified.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task SendLoginRequestAsync(BackchannelUserLoginRequest request, Ct ct);
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.FederatedSignOut;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ProcessingSAML2IdPInitiatedFederatedSignout),
        Message = "Processing SAML2 IdP-initiated federated signout")]
    internal static partial void ProcessingSAML2IdPInitiatedFederatedSignout(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoDownstreamClientsToNotifyRedirectingToCompletion),
        Message = "No downstream clients to notify, redirecting to completion endpoint")]
    internal static partial void NoDownstreamClientsToNotifyRedirectingToCompletion(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(StoredSAMLLogoutContextRenderingCombinedPage),
        Message = "Stored SAML logout context, rendering combined page")]
    internal static partial void StoredSAMLLogoutContextRenderingCombinedPage(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(CannotRenderCombinedLogoutPageResponseHasAlready),
        Message = "Cannot render combined logout page: response has already started. " +
                "The upstream IdP will not receive a LogoutResponse for this session")]
    internal static partial void CannotRenderCombinedLogoutPageResponseHasAlready(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(CannotRenderCombinedLogoutPageResponseDestinationIsNot),
        Message = "Cannot render combined logout page: ResponseDestination is not a valid URI: {Destination}")]
    internal static partial void CannotRenderCombinedLogoutPageResponseDestinationIsNot(this ILogger logger, object destination);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ProcessingFederatedSignout),
        Message = "Processing federated signout")]
    internal static partial void ProcessingFederatedSignout(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RenderingSignoutCallbackIframe),
        Message = "Rendering signout callback iframe")]
    internal static partial void RenderingSignoutCallbackIframe(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoSignoutCallbackIframeToRender),
        Message = "No signout callback iframe to render")]
    internal static partial void NoSignoutCallbackIframeToRender(this ILogger logger);
}

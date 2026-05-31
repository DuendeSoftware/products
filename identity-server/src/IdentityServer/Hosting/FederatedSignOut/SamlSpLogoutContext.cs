// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Hosting.FederatedSignOut;

/// <summary>
/// Captures SAML SP-side logout context from the LogoutResponseCreated notification.
/// Stored in HttpContext.Items to signal that an IdP-initiated SAML logout is in progress.
/// </summary>
internal sealed class SamlSpLogoutContext
{
    /// <summary>
    /// The HttpContext.Items key used to store this context.
    /// </summary>
    internal const string HttpContextItemsKey = "IdentityServer.SamlSpLogoutContext";

    /// <summary>
    /// The upstream IdP's entity ID.
    /// </summary>
    public required string IdpEntityId { get; init; }

    /// <summary>
    /// The ID of the LogoutRequest from the upstream IdP.
    /// </summary>
    public required string LogoutRequestId { get; init; }

    /// <summary>
    /// The RelayState to include in the LogoutResponse.
    /// </summary>
    public string? RelayState { get; init; }

    /// <summary>
    /// The binding type to use for the LogoutResponse (e.g., HttpRedirect or HttpPost).
    /// </summary>
    public required string ResponseBinding { get; init; }

    /// <summary>
    /// The destination URL for the LogoutResponse.
    /// </summary>
    public required string ResponseDestination { get; init; }

    /// <summary>
    /// Stores a <see cref="SamlSpLogoutContext"/> in <see cref="HttpContext.Items"/>
    /// from the parameters available in the LogoutResponseCreated notification.
    /// Called by both the static and dynamic SAML SP configuration paths.
    /// </summary>
    internal static void SetFromNotification(
        HttpContext? httpContext,
        string idpEntityId,
        string logoutRequestId,
        string? relayState,
        string responseBinding,
        Uri? responseUrl)
    {
        if (httpContext == null || responseUrl == null)
        {
            return;
        }

        httpContext.Items[HttpContextItemsKey] = new SamlSpLogoutContext
        {
            IdpEntityId = idpEntityId,
            LogoutRequestId = logoutRequestId,
            RelayState = relayState,
            ResponseBinding = responseBinding,
            ResponseDestination = responseUrl.AbsoluteUri
        };
    }
}

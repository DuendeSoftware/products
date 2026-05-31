// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Hosting.FederatedSignOut;

/// <summary>
/// Internal message stored during SAML SP-initiated federated sign-out.
/// Captures the information needed to generate a LogoutResponse back to the
/// upstream IdP after downstream clients have been notified.
/// </summary>
internal sealed record SamlSpLogoutMessage
{
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
    /// The subject ID of the user being logged out.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// The session ID of the user being logged out.
    /// </summary>
    public string? SessionId { get; init; }
}

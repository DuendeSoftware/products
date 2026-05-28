// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using SamlLogoutRequest = Duende.IdentityServer.Saml.Samlp.LogoutRequest;

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// Validated LogoutRequest context
/// </summary>
public class ValidatedLogoutRequest
{
    /// <summary>
    /// The parsed LogoutRequest
    /// </summary>
    public required SamlLogoutRequest LogoutRequest { get; init; }

    /// <summary>
    /// Identifier of binding used to receive the LogoutRequest
    /// </summary>
    public required string Binding { get; init; }

    /// <summary>
    /// The raw inbound SAML message from the binding layer.
    /// Null when the request is reconstructed from stored state (e.g., in the callback endpoint).
    /// </summary>
    public InboundSaml2Message? Saml2Message { get; init; }

    /// <summary>
    /// The relay state to include in the response.
    /// When <see cref="Saml2Message"/> is present, this defaults to <see cref="Saml2Message.RelayState"/>.
    /// </summary>
    public string? RelayState { get; init; }

    /// <summary>
    /// The resolved SAML Service Provider
    /// </summary>
    public SamlServiceProvider? Saml2Sp { get; set; }

    /// <summary>
    /// The current user
    /// </summary>
    public ClaimsPrincipal? Subject { get; init; }

    /// <summary>
    /// The current session ID
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// The SAML entity ID of this IdP
    /// </summary>
    public required string Saml2IdpEntityId { get; init; }
}

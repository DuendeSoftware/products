// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Bindings;

namespace Duende.IdentityServer.Saml.Services;

/// <summary>
/// Encapsulates the result of building a SAML LogoutRequest, including the binding-layer
/// message and application-level metadata needed for response correlation.
/// </summary>
/// <param name="Message">The binding-layer message ready to be sent to the SP.</param>
/// <param name="RequestId">The SAML request ID (from the <c>ID</c> attribute) for correlating responses via <c>InResponseTo</c>.</param>
/// <param name="SpEntityId">The entity ID of the SP this request is destined for.</param>
public sealed record SamlLogoutRequestContext(
    OutboundSaml2Message Message,
    string RequestId,
    string SpEntityId);

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml.Services;

/// <summary>
/// Builds outbound SAML 2.0 LogoutRequest messages for front-channel logout notifications.
/// </summary>
public interface ISaml2FrontChannelLogoutRequestBuilder
{
    /// <summary>
    /// Builds a front-channel logout request for the given service provider.
    /// </summary>
    /// <param name="serviceProvider">The SP to notify.</param>
    /// <param name="nameId">The NameID value for the subject being logged out.</param>
    /// <param name="nameIdFormat">Optional NameID format URI.</param>
    /// <param name="sessionIndex">The session index to include in the request.</param>
    /// <param name="issuer">The IdP entity ID (issuer).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SamlLogoutRequestContext"/> containing the message and correlation metadata.</returns>
    Task<SamlLogoutRequestContext> BuildLogoutRequestAsync(
        SamlServiceProvider serviceProvider,
        string nameId,
        string? nameIdFormat,
        string sessionIndex,
        string issuer,
        Ct ct);
}

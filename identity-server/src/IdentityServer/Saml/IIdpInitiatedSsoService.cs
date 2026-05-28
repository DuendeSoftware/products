// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Service for generating IdP-initiated SSO responses. The host calls this from
/// a portal page (or similar UI) where the user is already authenticated.
/// The service validates the target SP, generates a signed SAML response, and
/// returns an <see cref="IdpInitiatedSsoResult"/> that either contains an
/// <see cref="IResult"/> the host returns from its endpoint, or an error the
/// host can display in its portal UI.
/// </summary>
/// <remarks>
/// <para>
/// Because the user is already authenticated when this service is called, the
/// IdP-initiated flow does not redirect to the login page. The SAML response is
/// generated immediately and sent via the appropriate binding (e.g., HTTP-POST).
/// </para>
/// <para>
/// <strong>Anti-forgery:</strong> This service does not perform anti-forgery
/// validation itself. The caller (typically a Razor Page or MVC action) is
/// responsible for protecting the endpoint that invokes this service with
/// standard ASP.NET Core anti-forgery tokens or equivalent protection.
/// </para>
/// <para>
/// <strong>Replay protection:</strong> IdP-initiated SSO responses do not
/// contain an <c>InResponseTo</c> attribute because there is no prior
/// AuthnRequest to reference. This is inherent to the IdP-initiated profile
/// (SAML Profiles §4.1.4.5). Service providers are responsible for enforcing
/// one-time use of assertion IDs to mitigate replay attacks.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Razor Page handler — anti-forgery is enforced by default on POST handlers
/// public async Task&lt;IResult&gt; OnPostAsync(string spEntityId, string? relayState)
/// {
///     var result = await _idpInitiatedSso.CreateResponseAsync(HttpContext, spEntityId, relayState, HttpContext.RequestAborted);
///     if (result.IsError)
///     {
///         ErrorMessage = result.Error;
///         return new PageResult();
///     }
///     return result.Response!;
/// }
/// </code>
/// </example>
public interface IIdpInitiatedSsoService
{
    /// <summary>
    /// Creates a SAML response for IdP-initiated SSO to the specified service provider.
    /// </summary>
    /// <param name="httpContext">The current HTTP context, used to resolve services and
    /// write the response via the binding.</param>
    /// <param name="spEntityId">The entity ID of the target service provider.</param>
    /// <param name="relayState">Optional relay state to include in the SAML response.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing either an <see cref="IResult"/> that writes the SAML
    /// response via the binding, or an error with a descriptive message.</returns>
    Task<IdpInitiatedSsoResult> CreateResponseAsync(
        HttpContext httpContext,
        string spEntityId,
        string? relayState,
        Ct ct);

    /// <summary>
    /// Creates a SAML response for IdP-initiated SSO to the specified service provider,
    /// without a relay state.
    /// </summary>
    /// <param name="httpContext">The current HTTP context, used to resolve services and
    /// write the response via the binding.</param>
    /// <param name="spEntityId">The entity ID of the target service provider.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing either an <see cref="IResult"/> that writes the SAML
    /// response via the binding, or an error with a descriptive message.</returns>
    Task<IdpInitiatedSsoResult> CreateResponseAsync(
        HttpContext httpContext,
        string spEntityId,
        Ct ct);
}

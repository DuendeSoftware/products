// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the response returned from the backchannel authentication endpoint as part of the
/// Client-Initiated Backchannel Authentication (CIBA) flow. The response contains the
/// <c>auth_req_id</c> that the client uses to poll the token endpoint, along with the expiration
/// and polling interval. This interface is invoked after the backchannel authentication request
/// has been validated and the authentication request has been stored.
/// </summary>
/// <remarks>
/// The default implementation creates and stores the backchannel authentication request and
/// returns the appropriate identifiers and timing parameters. Override this interface or extend
/// the default implementation to customize the backchannel authentication response, for example
/// to adjust expiration times or add custom response properties.
/// </remarks>
public interface IBackchannelAuthenticationResponseGenerator
{
    /// <summary>
    /// Processes a validated backchannel authentication request and produces the CIBA response.
    /// </summary>
    /// <param name="validationResult">
    /// The result of validating the backchannel authentication request, including the client,
    /// requested scopes, login hint, and binding message.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="BackchannelAuthenticationResponse"/> containing the <c>auth_req_id</c>,
    /// expiration time, and polling interval that the client uses to retrieve tokens once the
    /// user has authenticated.
    /// </returns>
    Task<BackchannelAuthenticationResponse> ProcessAsync(BackchannelAuthenticationRequestValidationResult validationResult, Ct ct);
}

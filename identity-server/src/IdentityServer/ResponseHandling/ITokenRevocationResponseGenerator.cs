// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the response for the token revocation endpoint (RFC 7009) and performs the actual
/// revocation of the presented token. When a valid access token or refresh token is submitted,
/// this generator revokes it (and, for refresh tokens, any associated access tokens) and
/// produces the appropriate HTTP response. This interface is invoked after the revocation
/// request has been validated.
/// </summary>
/// <remarks>
/// The default implementation revokes the token from the token store and returns the
/// appropriate success or no-op response as required by RFC 7009. Override this interface or
/// extend the default implementation to add custom revocation logic, for example to propagate
/// revocation to external systems or to audit revocation events.
/// </remarks>
public interface ITokenRevocationResponseGenerator
{
    /// <summary>
    /// Processes a validated token revocation request, revokes the token, and produces the
    /// revocation endpoint response.
    /// </summary>
    /// <param name="validationResult">
    /// The result of validating the revocation request, including the token to revoke and the
    /// client that submitted the request.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="TokenRevocationResponse"/> describing the outcome of the revocation
    /// operation.
    /// </returns>
    Task<TokenRevocationResponse> ProcessAsync(TokenRevocationRequestValidationResult validationResult, Ct ct);
}

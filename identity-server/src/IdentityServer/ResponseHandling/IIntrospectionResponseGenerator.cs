// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the response returned from the token introspection endpoint (RFC 7662). The
/// response describes whether a presented token is active and, if so, includes the token's
/// claims and metadata such as scope, subject, client ID, and expiration. This interface is
/// invoked after the introspection request has been validated and the caller's identity has
/// been confirmed.
/// </summary>
/// <remarks>
/// The default implementation resolves the token from the token store, verifies that the
/// calling API resource is allowed to introspect it, and returns the appropriate claims.
/// Override this interface or extend the default implementation to customize the set of claims
/// returned in the introspection response, for example to add or suppress specific claims for
/// particular API resources.
/// </remarks>
public interface IIntrospectionResponseGenerator
{
    /// <summary>
    /// Processes a validated introspection request and produces the introspection response.
    /// </summary>
    /// <param name="validationResult">
    /// The result of validating the introspection request, including the token being introspected
    /// and the API resource that submitted the request.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A dictionary of claim names to claim values that will be serialized as JSON and returned
    /// from the introspection endpoint. An inactive token results in a response containing only
    /// <c>active: false</c>.
    /// </returns>
    Task<Dictionary<string, object>> ProcessAsync(IntrospectionRequestValidationResult validationResult, Ct ct);
}

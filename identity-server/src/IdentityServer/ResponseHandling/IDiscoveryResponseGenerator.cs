// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the responses for the OpenID Connect discovery endpoint
/// (<c>/.well-known/openid-configuration</c>) and the JSON Web Key Set endpoint
/// (<c>/.well-known/openid-configuration/jwks</c>). The discovery document advertises the
/// server's capabilities, supported grant types, endpoints, and signing algorithms. The JWK
/// document exposes the public keys used to verify tokens issued by this server.
/// </summary>
/// <remarks>
/// The default implementation builds the discovery document from the configured
/// <see cref="IdentityServerOptions"/>, registered resources, and available signing credentials.
/// Override this interface or extend the default implementation to add custom claims or
/// metadata to the discovery document, for example to advertise proprietary extensions.
/// </remarks>
public interface IDiscoveryResponseGenerator
{
    /// <summary>
    /// Creates the OpenID Connect discovery document that describes this server's endpoints,
    /// supported grant types, response types, scopes, and other capabilities.
    /// </summary>
    /// <param name="baseUrl">The base URL of the IdentityServer instance.</param>
    /// <param name="issuerUri">The issuer URI that identifies this authorization server.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A dictionary of discovery document entries that will be serialized as JSON and returned
    /// from the <c>/.well-known/openid-configuration</c> endpoint.
    /// </returns>
    Task<Dictionary<string, object>> CreateDiscoveryDocumentAsync(string baseUrl, string issuerUri, Ct ct);

    /// <summary>
    /// Creates the JSON Web Key Set document that exposes the public signing keys used by this
    /// server to sign tokens.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="JsonWebKey"/> objects representing the server's
    /// active public signing keys, returned from the JWKS endpoint.
    /// </returns>
    Task<IReadOnlyCollection<JsonWebKey>> CreateJwkDocumentAsync(Ct ct);
}

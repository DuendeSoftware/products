// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Responsible for building the <see cref="Token"/> model for identity tokens and access tokens.
/// This is a higher-level service than <see cref="ITokenCreationService"/>: it assembles the
/// token's claims, lifetime, and signing key information, then delegates serialization to
/// <see cref="ITokenCreationService"/>. Implement or override this service to customize how
/// token models are constructed before they are signed and serialized.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates an identity token model for the given request.
    /// The resulting <see cref="Token"/> contains the user's identity claims and is intended
    /// to be serialized into a signed JWT by <see cref="ITokenCreationService"/>.
    /// </summary>
    /// <param name="request">
    /// The token creation request containing the subject, client, requested resources,
    /// and other parameters needed to build the identity token.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="Token"/> model representing the identity token, ready to be serialized
    /// by <see cref="CreateSecurityTokenAsync"/>.
    /// </returns>
    Task<Token> CreateIdentityTokenAsync(TokenCreationRequest request, Ct ct);

    /// <summary>
    /// Creates an access token model for the given request.
    /// The resulting <see cref="Token"/> contains the authorized scopes and claims and is intended
    /// to be serialized into a signed JWT or stored as a reference token.
    /// </summary>
    /// <param name="request">
    /// The token creation request containing the subject, client, requested resources,
    /// and other parameters needed to build the access token.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="Token"/> model representing the access token, ready to be serialized
    /// by <see cref="CreateSecurityTokenAsync"/>.
    /// </returns>
    Task<Token> CreateAccessTokenAsync(TokenCreationRequest request, Ct ct);

    /// <summary>
    /// Serializes and protects the given <see cref="Token"/> model into its wire format.
    /// For JWT tokens this produces a compact signed JWT string; for reference tokens
    /// the token is stored in the grant store and the returned value is the reference handle.
    /// </summary>
    /// <param name="token">The token model to serialize and protect.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The serialized token string to return to the client — either a compact JWT or
    /// an opaque reference token handle, depending on the token's access token type.
    /// </returns>
    Task<string> CreateSecurityTokenAsync(Token token, Ct ct);
}

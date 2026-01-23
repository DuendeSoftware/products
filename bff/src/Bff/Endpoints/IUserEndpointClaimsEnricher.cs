// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;

namespace Duende.Bff.Endpoints;

/// <summary>
/// Optional interface, that allows users to enrich or replace the claims returned from the user endpoint.
///
/// Asp.net already has a concept of claims transformation via IClaimsTransformation. However, this
/// happens DURING the authentication process. At that point in time, there is no authenticated principal
/// yet. This means, that if you need to call external services to get more data for the user, using the
/// access token from the authentication, that is not possible.
///
/// This endpoint gives you access to the AuthenticateResult including the access token, so you can use
/// AccessTokenManagement to get tokens for calling APIs to get more user data.
/// </summary>
public interface IUserEndpointClaimsEnricher
{
    /// <summary>
    /// Enrich the claims for the user endpoint. You can return the same claims, a modified set of claims, 
    /// or completely new claims.
    /// </summary>
    /// <param name="authenticateResult">The result from the authentication endpoint. </param>
    /// <param name="claims">The current set of claims to be returned. </param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The updated list of claims.</returns>
    Task<IReadOnlyList<ClaimRecord>> EnrichClaimsAsync(AuthenticateResult authenticateResult, IReadOnlyList<ClaimRecord> claims, CT ct = default);
}

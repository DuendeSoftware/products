// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Models making HTTP requests for JWTs from the authorize endpoint.
/// </summary>
public interface IJwtRequestUriHttpClient
{
    /// <summary>
    /// Gets a JWT from the url.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="client"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<string?> GetJwtAsync(string url, Client client, Ct ct);
}

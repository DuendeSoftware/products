// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Services;

/// <summary>
/// Determines whether cross-origin requests from a given origin are permitted to access
/// IdentityServer protocol endpoints. Implement this interface to apply custom CORS origin
/// validation logic.
/// </summary>
/// <remarks>
/// The built-in <c>DefaultCorsPolicyService</c> checks against an explicit allow-list
/// (or allows all origins). The Entity Framework and in-memory store implementations check
/// whether the origin matches any of the allowed CORS origins configured on registered clients.
/// </remarks>
public interface ICorsPolicyService
{
    /// <summary>
    /// Determines whether the specified origin is allowed to make cross-origin requests
    /// to IdentityServer protocol endpoints.
    /// </summary>
    /// <param name="origin">The origin to evaluate (e.g., <c>https://app.example.com</c>).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if the origin is permitted; otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> IsOriginAllowedAsync(string origin, Ct ct);
}

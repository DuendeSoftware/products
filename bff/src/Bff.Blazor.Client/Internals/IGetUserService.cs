// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;

namespace Duende.Bff.Blazor.Client.Internals;

/// <summary>
/// Internal service for retrieval of user info in the authentication state provider.
/// </summary>
public interface IGetUserService
{
    /// <summary>
    /// Gets the user.
    /// </summary>
    ValueTask<ClaimsPrincipal> GetUserAsync(bool useCache = true);

    /// <summary>
    /// Initializes the cache.
    /// </summary>
    void InitializeCache();
}
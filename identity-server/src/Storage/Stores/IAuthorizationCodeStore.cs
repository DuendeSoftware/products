// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for the authorization code store
/// </summary>
public interface IAuthorizationCodeStore
{
    /// <summary>
    /// Stores the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code);

    /// <summary>
    /// Gets the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code);

    /// <summary>
    /// Removes the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    Task RemoveAuthorizationCodeAsync(string code);
}

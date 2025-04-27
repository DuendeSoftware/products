// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Interface for the return URL parser
/// </summary>
public interface IReturnUrlParser
{
    /// <summary>
    /// Parses a return URL.
    /// </summary>
    /// <param name="returnUrl">The return URL.</param>
    Task<AuthorizationRequest?> ParseAsync(string returnUrl);

    /// <summary>
    /// Determines whether the return URL is valid.
    /// </summary>
    /// <param name="returnUrl">The return URL.</param>
    /// <returns>
    ///   <c>true</c> if the return URL is valid; otherwise, <c>false</c>.
    /// </returns>
    bool IsValidReturnUrl(string returnUrl);
}

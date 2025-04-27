// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Parser for finding the best secret in an Enumerable List
/// </summary>
public interface ISecretsListParser
{
    /// <summary>
    /// Tries to find the best secret on the context that can be used for authentication
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>
    /// A parsed secret
    /// </returns>
    Task<ParsedSecret?> ParseAsync(HttpContext context);

    /// <summary>
    /// Gets all available authentication methods.
    /// </summary>
    IEnumerable<string> GetAvailableAuthenticationMethods();
}

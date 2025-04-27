// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Hosting;

/// <summary>
/// Endpoint handler
/// </summary>
public interface IEndpointHandler
{
    /// <summary>
    /// Processes the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    Task<IEndpointResult?> ProcessAsync(HttpContext context);
}

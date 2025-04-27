// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.Endpoints;

/// <summary>
/// Service definition for handling endpoint requests
/// </summary>
public interface IBffEndpointService
{
    /// <summary>
    /// Process a request
    /// </summary>
    /// <param name="context"></param>
    Task ProcessRequestAsync(HttpContext context);
}

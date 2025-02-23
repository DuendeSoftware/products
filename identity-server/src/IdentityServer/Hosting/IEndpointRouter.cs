// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Hosting;

/// <summary>
/// The endpoint router is responsible for mapping incoming http requests onto
/// <see cref="IEndpointHandler"/>s, for the protocol endpoints that
/// IdentityServer supports.
/// </summary>
public interface IEndpointRouter
{
    /// <summary>
    /// Finds a matching <see cref="IEndpointHandler"/> for an incoming http
    /// request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The handler to process a protocol request, or null, if the
    /// incoming http request is not a protocol request.</returns>
    IEndpointHandler? Find(HttpContext context);
}

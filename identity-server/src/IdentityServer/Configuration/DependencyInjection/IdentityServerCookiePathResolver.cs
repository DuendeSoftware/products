// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Resolves the request-scoped cookie path that IdentityServer uses for its cookies.
/// </summary>
internal static class IdentityServerCookiePathResolver
{
    /// <summary>
    /// Resolves the cookie path for the current request from <see cref="IServerUrls"/>.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>The cleaned up cookie path.</returns>
    public static string Resolve(HttpContext context)
    {
        var urls = context.RequestServices.GetRequiredService<IServerUrls>();
        return urls.BasePath.CleanUrlPath();
    }
}

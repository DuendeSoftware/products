// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Represents the endpoint that retrieves the index html
/// See https://duende.link/d/bff/ui-hosting for more information
/// </summary>
public interface IStaticFilesClient
{
    /// <summary>
    /// This method retrieves the index.html from the configured CDN URL for the current frontend.
    /// This response may be cached for increased performance but the cached response for a given frontend
    /// will be cleared when that frontend is updated in the <see cref="IFrontendCollection"/>
    /// 
    /// See https://duende.link/d/bff/ui-hosting for more information
    /// </summary>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Index HTML</returns>
    Task<string?> GetIndexHtmlAsync(CT ct = default);

    /// <summary>
    /// This method proxies all static asset requests to the configured CDN URL for the current frontend.
    ///
    /// This feature is mostly useful during development when you want to serve the static assets
    /// from the local development server. It can be used in production, however, proxying all static
    /// files through the BFF server is not optimal for performance and scalability.
    /// 
    /// See https://duende.link/d/bff/ui-hosting for more information
    /// </summary>
    /// <param name="context">HttpContext</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    Task ProxyStaticAssetsAsync(HttpContext context, CT ct = default);
}

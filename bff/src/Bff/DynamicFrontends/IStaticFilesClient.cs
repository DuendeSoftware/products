// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Represents the endpoint that retrieves the index html
/// </summary>
public interface IStaticFilesClient
{
    /// <summary>
    /// This method retrieves the index.html from the configured CDN URL for the current frontend.
    /// This response may be cached for increased performance but it's cache will be cleared. 
    /// </summary>
    /// <param name="ct"></param>
    /// <returns>Index HTML</returns>
    Task<string?> GetIndexHtmlAsync(CT ct = default);

    /// <summary>
    /// This method proxies all static asset requests to the configured CDN URL for the current frontend.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ProxyStaticAssetsAsync(HttpContext context, CT ct = default);
}

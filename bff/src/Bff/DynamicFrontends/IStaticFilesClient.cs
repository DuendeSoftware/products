// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Represents the endpoint that retrieves the index html
/// </summary>
public interface IStaticFilesClient
{
    Task<string?> GetIndexHtmlAsync(CT ct = default);
    Task ProxyStaticAssetsAsync(HttpContext context, CT ct = default);
}

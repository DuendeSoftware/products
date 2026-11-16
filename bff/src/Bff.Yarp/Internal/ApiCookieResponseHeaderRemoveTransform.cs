// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace Duende.Bff.Yarp.Internal;

/// <summary>
/// Removes the <c>Set-Cookie</c> and <c>Clear-Site-Data</c> values that the proxied API sent
/// from the response to the browser. Values that the BFF wrote itself in the same request stay.
/// </summary>
internal class ApiCookieResponseHeaderRemoveTransform(IOptions<BffOptions> options) : ResponseTransform
{
    private static readonly string[] s_headerNames = [HeaderNames.SetCookie, "Clear-Site-Data"];

    /// <inheritdoc />
    public override ValueTask ApplyAsync(ResponseTransformContext context)
    {
        if (!options.Value.RemoveCookieHeadersFromProxiedResponses || context.ProxyResponse is null || !context.HeadersCopied)
        {
            return default;
        }

        var responseHeaders = context.HttpContext.Response.Headers;
        foreach (var headerName in s_headerNames)
        {
            if (!context.ProxyResponse.Headers.NonValidated.TryGetValues(headerName, out var apiValues))
            {
                continue;
            }

            var remainingValues = responseHeaders[headerName].ToList();
            foreach (var apiValue in apiValues)
            {
                _ = remainingValues.Remove(apiValue);
            }

            if (remainingValues.Count == 0)
            {
                _ = responseHeaders.Remove(headerName);
            }
            else
            {
                responseHeaders[headerName] = new StringValues(remainingValues.ToArray());
            }
        }

        return default;
    }
}

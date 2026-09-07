// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Duende.IdentityServer.Internal.Saml.Sp.AspNetCore
{

    /// <summary>
    /// Extensions methods for Asp.Net Core Http Request.
    /// </summary>
    internal static class HttpRequestExtensions
    {
        extension(HttpContext httpContext)
        {
            /// <summary>
            /// Create an internal HttpRequestData from the Asp.Net Core
            /// HttpRequest
            /// </summary>
            /// <param name="cookieManager">Cookie manager to use to read cookies</param>
            /// <param name="cookieDecryptor">Decryptor for encrypted cookie data</param>
            /// <returns></returns>
            public HttpRequestData ToHttpRequestData(
                ICookieManager cookieManager,
                Func<byte[], byte[]> cookieDecryptor)
            {
                var request = httpContext.Request;

                var uri = new Uri(UriHelper.GetEncodedUrl(request));

                var pathBase = httpContext.Request.PathBase.Value;
                pathBase = string.IsNullOrEmpty(pathBase) ? "/" : pathBase;
                IEnumerable<KeyValuePair<string, IEnumerable<string>>> formData = null;
                if (httpContext.Request.Method == "POST" && httpContext.Request.HasFormContentType)
                {
                    formData = request.Form.Select(
                        f => new KeyValuePair<string, IEnumerable<string>>(f.Key, f.Value));
                }

                return new HttpRequestData(
                    httpContext.Request.Method,
                    uri,
                    pathBase,
                    formData,
                    cookieName => cookieManager.GetRequestCookie(httpContext, cookieName),
                    cookieDecryptor,
                    httpContext.User);
            }
        }

        extension(HttpRequest request)
        {
            /// <summary>
            /// Get the user agent.
            /// </summary>
            /// <returns></returns>
            public string GetUserAgent()
            {
                return request.Headers["user-agent"].FirstOrDefault() ?? "";
            }
        }
    }
}

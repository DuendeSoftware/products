// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extension methods for IServerUrls
/// </summary>
public static class ServerUrlExtensions
{
    /// <summary>
    /// Returns the origin in unicode, and not in punycode (if we have a unicode hostname)
    /// </summary>
    public static string GetUnicodeOrigin(this IServerUrls urls)
    {
        var split = urls.Origin.Split("://", StringSplitOptions.RemoveEmptyEntries);
        var scheme = split.First();
        var host = HostString.FromUriComponent(split.Last()).Value;
            
        return scheme + "://" + host;
    }

    /// <summary>
    /// Returns an absolute URL for the URL or path.
    /// </summary>
    public static string GetAbsoluteUrl(this IServerUrls urls, string urlOrPath)
    {
        if (urlOrPath.IsLocalUrl())
        {
            if (urlOrPath.StartsWith("~/")) urlOrPath = urlOrPath.Substring(1);
            urlOrPath = urls.BaseUrl + urlOrPath.EnsureLeadingSlash();
        }
        return urlOrPath;
    }


    /// <summary>
    /// Returns the URL into the server based on the relative path. The path parameter can start with "~/" or "/".
    /// </summary>
    public static string GetIdentityServerRelativeUrl(this IServerUrls urls, string path)
    {
        if (!path.IsLocalUrl())
        {
            return null;
        }

        if (path.StartsWith("~/")) path = path.Substring(1);
        path = urls.BaseUrl + path.EnsureLeadingSlash();
        return path;
    }
}
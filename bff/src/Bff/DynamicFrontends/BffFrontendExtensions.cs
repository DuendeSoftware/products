// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

public static class BffFrontendExtensions
{
    public static BffFrontend WithIndexHtmlUrl(this BffFrontend frontend, Uri? url)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            IndexHtmlUrl = url
        };
    }

    public static BffFrontend WithOpenIdConnectOptions(this BffFrontend frontend, Action<OpenIdConnectOptions> options)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            ConfigureOpenIdConnectOptions = options
        };
    }

    public static BffFrontend WithCookieOptions(this BffFrontend frontend, Action<CookieAuthenticationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            ConfigureCookieOptions = options
        };
    }

    /// <summary>
    /// Map a frontend to a given URI. You can provide a url with or without a path.
    ///
    /// If you provide a url with a path, then it will only be selected if both the host header and path matches.
    /// Otherwise, it will be selected based on host header only.
    /// 
    /// Frontend selection happens from most specific to least specific.
    /// 
    /// Note, if you have previously configured selection criteria (host header or path) on the frontend, then this method will
    /// throw an exception, unless the <paramref name="force"/> parameter is set to true.
    /// </summary>
    /// <param name="frontend">The frontend to map</param>
    /// <param name="uri">The URI to match the host header value to.</param>
    /// <param name="force">Should existing selection criteria be overwritten?</param>
    /// <returns></returns>
    public static BffFrontend MapTo(this BffFrontend frontend, Uri uri, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(frontend);

        GuardOverrideSelectionCriteria(frontend, force);

        PathString? path = null;
        
        if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
        {
            path = uri.AbsolutePath;
        }
        
        return frontend with
            {
                MatchingCriteria = frontend.MatchingCriteria with
                {
                    MatchingHostHeader = HostHeaderValue.Parse(new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri),
                    MatchingPath = path
                }
            };
    }

    private static void GuardOverrideSelectionCriteria(BffFrontend frontend, bool force)
    {
        if (frontend.MatchingCriteria.HasValue && !force)
        {
            throw new InvalidOperationException(
                $"Frontend {frontend.Name} already has selection criteria. Use the {nameof(force)} parameter to override this value.");
        }
    }

    /// <summary>
    /// Map this frontend to a given host header value and path.
    /// This means the frontend is only selected if both the host header and path matches.
    ///
    /// Frontend selection happens from most specific to least specific. 
    /// 
    /// Note, if you have previously configured selection criteria (host header or path) on the frontend, then this method will
    /// throw an exception, unless the <paramref name="force"/> parameter is set to true.
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="matchingHost"></param>
    /// <param name="path"></param>
    /// <param name="force">Should existing selection criteria be overwritten?</param>
    /// <returns></returns>
    public static BffFrontend MapTo(this BffFrontend frontend, HostHeaderValue matchingHost, PathString path, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        GuardOverrideSelectionCriteria(frontend, force);

        return frontend with
        {
            MatchingCriteria = frontend.MatchingCriteria with
            {
                MatchingHostHeader = matchingHost,
                MatchingPath = path
            }
        };
    }

    /// <summary>
    /// Maps a frontend to a given host header value. Paths will not be used to select this frontend.
    ///
    /// Frontend selection happens from most specific to least specific. 
    /// 
    /// Note, if you have previously configured selection criteria (host header or path) on the frontend, then this method will
    /// throw an exception, unless the <paramref name="force"/> parameter is set to true.
    ///
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="matchingHost"></param>
    /// <param name="force">Should existing selection criteria be overwritten?</param>
    /// <returns></returns>
    public static BffFrontend MapToHost(this BffFrontend frontend, HostHeaderValue matchingHost, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        GuardOverrideSelectionCriteria(frontend, force);

        return frontend with
        {
            MatchingCriteria = frontend.MatchingCriteria with
            {
                MatchingHostHeader = matchingHost,
                MatchingPath = null
            }
        };
    }

    /// <summary>
    /// Maps a frontend to a given path. Host header will not be used to select this frontend.
    ///     ///
    /// Frontend selection happens from most specific to least specific. 
    /// 
    /// Note, if you have previously configured selection criteria (host header or path) on the frontend, then this method will
    /// throw an exception, unless the <paramref name="force"/> parameter is set to true.
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="pathMatch">The path that must match for this frontend to be selected.</param>
    /// <param name="force">Should existing selection criteria be overwritten?</param>
    /// <returns></returns>
    public static BffFrontend MapToPath(this BffFrontend frontend, PathString pathMatch, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(frontend);

        GuardOverrideSelectionCriteria(frontend, force);

        return frontend with
        {
            MatchingCriteria = frontend.MatchingCriteria with
            {
                MatchingHostHeader = null,
                MatchingPath = pathMatch
            }
        };
    }
}

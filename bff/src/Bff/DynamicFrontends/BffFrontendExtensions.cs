// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

public static class BffFrontendExtensions
{
    /// <summary>
    /// Setting this value indicates that an index.html file should be proxied through the BFF. When this value is set, any
    /// unmatched request will be forwarded to this URL. Usually, this file resides in a CDN.
    ///
    /// Any unmatched request will be forwarded to this URL. This allows for SPAs that use client side routing.
    ///
    /// See https://duende.link/d/bff/ui-hosting for more information.
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="url">The URL where the BFF Frontend can retrieve the IndexHtml.</param>
    /// <returns></returns>
    public static BffFrontend WithCdnIndexHtmlUrl(this BffFrontend frontend, Uri? url)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            CdnIndexHtmlUrl = url
        };
    }


    /// <summary>
    /// Setting this value indicates that static assets (js, css, images etc) should be proxied through the BFF
    /// from the given URL. When this value is set, any unmatched request will be forwarded to this URL. Should the response
    /// be 404, then another attempt to forward this request to '/' is done. This allows for SPAs that use client side routing.
    ///
    /// This setting is usually used during development in combination with development webserver.
    ///
    /// Note, this is less than ideal for production scenarios, as it adds additional load to the BFF server.
    /// Consider deploying your assets to a CDN and use the <see cref="WithCdnIndexHtmlUrl"/>. If you want to switch this value
    /// depending on environment, consider using the <see cref="WithBffStaticAssets"/> extension method.
    ///
    /// See https://duende.link/d/bff/ui-hosting for more information.
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="url">The URL of your frontend development server, which serves the static assets.</param>
    /// <returns></returns>
    public static BffFrontend WithProxiedStaticAssets(this BffFrontend frontend, Uri url)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            StaticAssetsUrl = url
        };
    }

    /// <summary>
    /// Allows you to dynamically choose between using <see cref="WithCdnIndexHtmlUrl"/> and <see cref="WithProxiedStaticAssets"/>
    ///
    /// This is particularly useful when you want to use a CDN in production, but a proxied web server during development.
    /// The delegate could contain logic to determine the current environment.
    ///
    /// Important to note that this method will call the delegate during the configuration of the frontend. It's not
    /// evaluated at runtime.
    ///
    /// See https://duende.link/d/bff/ui-hosting for more information.
    ///
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="uri"></param>
    /// <param name="useCdnWhen">Func that sets if the CDN URL should be used or if all assets should be proxied. True for the <see cref="WithCdnIndexHtmlUrl"/>, false for <see cref="WithProxiedStaticAssets"/></param>
    /// <returns></returns>
    public static BffFrontend WithBffStaticAssets(this BffFrontend frontend, Uri uri, Func<bool> useCdnWhen)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        ArgumentNullException.ThrowIfNull(useCdnWhen);

        if (useCdnWhen())
        {
            return frontend.WithCdnIndexHtmlUrl(uri);
        }

        return frontend.WithProxiedStaticAssets(uri);
    }

    /// <summary>
    /// Configures the OpenID Connect options for the frontend. Any setting configured in this delegate overrides
    /// the defaults.
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static BffFrontend WithOpenIdConnectOptions(this BffFrontend frontend, Action<OpenIdConnectOptions> options)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            ConfigureOpenIdConnectOptions = options
        };
    }

    /// <summary>
    /// Configures the cookie authentication options for the frontend. Any setting configured in this delegate overrides
    /// </summary>
    /// <param name="frontend"></param>
    /// <param name="options"></param>
    /// <returns></returns>
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
        ArgumentNullException.ThrowIfNull(uri);

        GuardOverrideMatchingCriteria(frontend, force);

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

    private static void GuardOverrideMatchingCriteria(BffFrontend frontend, bool force)
    {
        if (frontend.MatchingCriteria.HasValue && !force)
        {
            throw new InvalidOperationException(
                $"Frontend {frontend.Name} already has matching criteria. Use the {nameof(force)} parameter to override this value.");
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
        GuardOverrideMatchingCriteria(frontend, force);

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
        GuardOverrideMatchingCriteria(frontend, force);

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
    ///
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

        GuardOverrideMatchingCriteria(frontend, force);

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

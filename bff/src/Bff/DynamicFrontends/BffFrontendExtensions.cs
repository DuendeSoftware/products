// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

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
    /// <param name="useCdnWhen">Func that sets if the CDN URL should be used or if all products should be proxied. True for the <see cref="WithCdnIndexHtmlUrl"/>, false for <see cref="WithProxiedStaticAssets"/></param>
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

    public static BffFrontend MappedToOrigin(this BffFrontend frontend, Origin origin)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            SelectionCriteria = frontend.SelectionCriteria with
            {
                MatchingOrigin = origin
            }
        };
    }

    public static BffFrontend MappedToPath(this BffFrontend frontend, LocalPath path)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend with
        {
            SelectionCriteria = frontend.SelectionCriteria with
            {
                MatchingPath = path
            }
        };
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Duende.Bff.DynamicFrontends;

public static class BffFrontendExtensions
{
    public static BffFrontend WithIndexHtmlUrl(this BffFrontend frontend, Uri? url) => frontend with
    {
        IndexHtmlUrl = url
    };
    public static BffFrontend WithOpenIdConnectOptions(this BffFrontend frontend, Action<OpenIdConnectOptions> options) => frontend with
    {
        ConfigureOpenIdConnectOptions = options
    };

    public static BffFrontend WithCookieOptions(this BffFrontend frontend, Action<CookieAuthenticationOptions> options) => frontend with
    {
        ConfigureCookieOptions = options
    };

    public static BffFrontend MappedToOrigin(this BffFrontend frontend, Origin origin) => frontend with
    {
        SelectionCriteria = frontend.SelectionCriteria with
        {
            MatchingOrigin = origin
        }
    };

    public static BffFrontend MappedToPath(this BffFrontend frontend, LocalPath path) => frontend with
    {
        SelectionCriteria = frontend.SelectionCriteria with
        {
            MatchingPath = path
        }
    };
}

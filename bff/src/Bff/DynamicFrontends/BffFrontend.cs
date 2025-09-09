// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Duende.Bff.DynamicFrontends;

public sealed record BffFrontend
{
    private readonly Uri? _staticAssetsUrl;
    private readonly Uri? _cdnIndexHtmlUrl;

    public BffFrontend()
    {
    }

    [SetsRequiredMembers]
    public BffFrontend(BffFrontendName name) => Name = name;

    public bool Equals(BffFrontend? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Name.Equals(other.Name) && SelectionCriteria.Equals(other.SelectionCriteria) && Equals(CdnIndexHtmlUrl, other.CdnIndexHtmlUrl) & DataExtensions.SequenceEqual(other.DataExtensions);
    }

    public override int GetHashCode() => HashCode.Combine(Name, SelectionCriteria, CdnIndexHtmlUrl, DataExtensions);

    public required BffFrontendName Name { get; init; }

    public Scheme CookieSchemeName => Scheme.Parse("cookie_" + Name);
    public Scheme OidcSchemeName => Scheme.Parse("oidc_" + Name);

    public Action<OpenIdConnectOptions>? ConfigureOpenIdConnectOptions { get; init; }

    public Action<CookieAuthenticationOptions>? ConfigureCookieOptions { get; init; }

    public FrontendSelectionCriteria SelectionCriteria { get; init; } = new();

    /// <summary>
    /// Setting this value indicates that an index.html file should be proxied through the BFF. When this value is set, any
    /// unmatched request will be forwarded to this URL. Usually, this file resides in a CDN.
    ///
    /// Any unmatched request will be forwarded to this URL. This allows for SPAs that use client side routing.
    /// </summary>
    public Uri? CdnIndexHtmlUrl
    {
        get => _cdnIndexHtmlUrl;
        init
        {
            if (StaticAssetsUrl != null)
            {
                throw new InvalidOperationException("Cannot use both StaticAssetsUrl and CdnIndexHtmlUrl");
            }
            _cdnIndexHtmlUrl = value;
        }
    }

    /// <summary>
    /// Setting this value indicates that static assets (js, css, images etc) should be proxied through the BFF
    /// from the given URL. When this value is set, any unmatched request will be forwarded to this URL. Should the response
    /// be 404, then another attempt to forward this request to '/' is done. This allows for SPAs that use client side routing.
    /// 
    /// This setting is usually used during development in combination with development webserver.
    ///
    /// Note, this is less than ideal for production scenarios, as it adds additional load to the BFF server.
    /// Consider deploying your assets to a CDN and use the <see cref="CdnIndexHtmlUrl"/>. If you want to switch this value
    /// depending on environment, consider using the <see cref="BffFrontendExtensions.WithStaticAssets"/> extension method.
    /// </summary>
    public Uri? StaticAssetsUrl
    {
        get => _staticAssetsUrl;
        init
        {
            if (CdnIndexHtmlUrl != null)
            {
                throw new InvalidOperationException("Cannot use both StaticAssetsUrl and CdnIndexHtmlUrl");
            }

            _staticAssetsUrl = value;
        }
    }

    internal IBffPlugin[] DataExtensions { get; init; } = [];

}

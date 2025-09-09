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

        return Name.Equals(other.Name) && SelectionCriteria.Equals(other.SelectionCriteria) && Equals(IndexHtmlUrl, other.IndexHtmlUrl) & DataExtensions.SequenceEqual(other.DataExtensions);
    }

    public override int GetHashCode() => HashCode.Combine(Name, SelectionCriteria, IndexHtmlUrl, DataExtensions);

    public required BffFrontendName Name { get; init; }

    public Scheme CookieSchemeName => Scheme.Parse("cookie_" + Name);
    public Scheme OidcSchemeName => Scheme.Parse("oidc_" + Name);

    public Action<OpenIdConnectOptions>? ConfigureOpenIdConnectOptions { get; init; }

    public Action<CookieAuthenticationOptions>? ConfigureCookieOptions { get; init; }

    public FrontendSelectionCriteria SelectionCriteria { get; init; } = new();

    public Uri? IndexHtmlUrl { get; init; }
    public Uri? StaticAssetsUrl { get; init; }

    internal IBffPlugin[] DataExtensions { get; init; } = [];

}

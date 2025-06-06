// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Configuration;

internal sealed record BffFrontendConfiguration
{
    public Uri? IndexHtmlUrl { get; init; }

    public string? MatchingPath { get; init; }

    public string? MatchingOrigin { get; init; }

    public OidcConfiguration? Oidc { get; init; }

    public CookieConfiguration? Cookies { get; init; }
}

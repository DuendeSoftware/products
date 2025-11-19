// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Configuration;

internal sealed record BffConfiguration
{
    public OidcConfiguration? DefaultOidcSettings { get; init; }

    public CookieConfiguration? DefaultCookieSettings { get; init; }

    public Dictionary<string, BffFrontendConfiguration> Frontends { get; init; } = new();
}

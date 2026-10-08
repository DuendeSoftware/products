// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Shared wildcard redirect URI detection used by both the FAPI 2.0 and OAuth 2.1 assessors.
/// IdentityServer compares redirect URIs by exact string match, so a '*' character is never a
/// real wildcard. A '*' in the path or query is matched literally. A '*' in the host makes the
/// URI unusable, because System.Uri rejects it and no real redirect can match it.
/// </summary>
internal static class RedirectUriWildcards
{
    public static bool HasWildcardHost(string redirectUri)
    {
        var schemeEnd = redirectUri.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
        {
            return false;
        }

        var authorityStart = schemeEnd + 3;
        var authorityEnd = redirectUri.IndexOfAny(['/', '?', '#'], authorityStart);
        var authority = authorityEnd < 0 ? redirectUri[authorityStart..] : redirectUri[authorityStart..authorityEnd];

        return authority.Contains('*', StringComparison.Ordinal);
    }
}

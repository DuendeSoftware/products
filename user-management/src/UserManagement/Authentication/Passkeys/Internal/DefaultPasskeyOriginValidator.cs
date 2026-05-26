// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Default origin validator that requires exact origin match against configured allowed origins
/// and rejects cross-origin requests.
/// </summary>
internal sealed class DefaultPasskeyOriginValidator : IPasskeyOriginValidator
{
    public ValueTask<bool> ValidateAsync(PasskeyOriginValidationContext context)
    {
        if (string.IsNullOrEmpty(context.Origin))
        {
            return ValueTask.FromResult(false);
        }

        if (context.CrossOrigin)
        {
            return ValueTask.FromResult(false);
        }

        if (!Uri.TryCreate(context.Origin, UriKind.Absolute, out var originUri))
        {
            return ValueTask.FromResult(false);
        }

        foreach (var allowedOrigin in context.AllowedOrigins)
        {
            if (!TryCreateAllowedOriginUri(allowedOrigin, out var allowedOriginUri))
            {
                continue;
            }

            if (OriginsMatch(originUri, allowedOriginUri))
            {
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }

    private static bool TryCreateAllowedOriginUri(string allowedOrigin, out Uri allowedOriginUri)
    {
        allowedOriginUri = null!;

        if (!Uri.TryCreate(allowedOrigin, UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(parsedUri.UserInfo) ||
            !string.IsNullOrEmpty(parsedUri.Query) ||
            !string.IsNullOrEmpty(parsedUri.Fragment))
        {
            return false;
        }

        if (parsedUri.AbsolutePath is not "/" and not "")
        {
            return false;
        }

        allowedOriginUri = parsedUri;
        return true;
    }

    private static bool OriginsMatch(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) &&
        a.Port == b.Port;
}

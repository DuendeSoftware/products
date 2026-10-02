// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// A <see cref="CookieBuilder"/> that delegates to an inner, user-configured builder and
/// overrides the resulting <see cref="CookieOptions.Path"/> with the request-scoped path
/// resolved by <see cref="IdentityServerCookiePathResolver"/>.
/// </summary>
/// <remarks>
/// Wrapping is resolved lazily, at <see cref="Build(HttpContext, DateTimeOffset)"/> time, so
/// that the path reflects the current request rather than the state at options-configuration
/// time (when options are typically configured once as singletons).
/// </remarks>
internal sealed class IdentityServerCookieBuilder : CookieBuilder
{
    private readonly CookieBuilder _inner;

    private IdentityServerCookieBuilder(CookieBuilder inner) => _inner = inner;

    /// <summary>
    /// Wraps the given <see cref="CookieBuilder"/> so that its built <see cref="CookieOptions"/>
    /// have their <see cref="CookieOptions.Path"/> replaced with the request-scoped IdentityServer
    /// cookie path. Wrapping an already-wrapped builder returns it unchanged.
    /// </summary>
    /// <param name="builder">The builder to wrap.</param>
    /// <returns>The wrapped builder.</returns>
    public static CookieBuilder Wrap(CookieBuilder builder) =>
        builder is IdentityServerCookieBuilder
            ? builder
            : new IdentityServerCookieBuilder(builder);

    /// <inheritdoc />
    public override string? Name
    {
        get => _inner.Name;
        set => _inner.Name = value;
    }

    /// <inheritdoc />
    public override string? Path
    {
        get => _inner.Path;
        set => _inner.Path = value;
    }

    /// <inheritdoc />
    public override string? Domain
    {
        get => _inner.Domain;
        set => _inner.Domain = value;
    }

    /// <inheritdoc />
    public override bool HttpOnly
    {
        get => _inner.HttpOnly;
        set => _inner.HttpOnly = value;
    }

    /// <inheritdoc />
    public override SameSiteMode SameSite
    {
        get => _inner.SameSite;
        set => _inner.SameSite = value;
    }

    /// <inheritdoc />
    public override CookieSecurePolicy SecurePolicy
    {
        get => _inner.SecurePolicy;
        set => _inner.SecurePolicy = value;
    }

    /// <inheritdoc />
    public override TimeSpan? Expiration
    {
        get => _inner.Expiration;
        set => _inner.Expiration = value;
    }

    /// <inheritdoc />
    public override TimeSpan? MaxAge
    {
        get => _inner.MaxAge;
        set => _inner.MaxAge = value;
    }

    /// <inheritdoc />
    public override bool IsEssential
    {
        get => _inner.IsEssential;
        set => _inner.IsEssential = value;
    }

    /// <inheritdoc />
    public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
    {
        var options = _inner.Build(context, expiresFrom);
        var path = IdentityServerCookiePathResolver.Resolve(context);

        ValidateHostPrefixCompatibility(_inner.Name, path);

        var result = new CookieOptions(options)
        {
            Path = path
        };

        // CookieBuilder.Extensions is a get-only, non-virtual property, so it cannot be
        // overridden. Extensions added to this wrapper (e.g. via a PostConfigure registered
        // after AddIdentityServer()) would otherwise be silently dropped, since _inner.Build()
        // above only sees extensions added to the inner builder. Merge them here instead,
        // without mutating _inner, so shared singleton state does not accumulate across requests.
        foreach (var extension in Extensions)
        {
            if (!result.Extensions.Contains(extension, StringComparer.Ordinal))
            {
                result.Extensions.Add(extension);
            }
        }

        return result;
    }

    /// <summary>
    /// Ensures a <c>__Host-</c> prefixed cookie name is only used when the resolved cookie path is
    /// exactly <c>/</c>, since the <c>__Host-</c> prefix requires the cookie to be scoped to the
    /// root path. Other cookie-prefix and Secure/Domain rules remain enforced by the underlying
    /// <see cref="CookieBuilder"/> and the framework.
    /// </summary>
    /// <param name="name">The configured cookie name.</param>
    /// <param name="resolvedPath">The request-scoped path that will be assigned to the cookie.</param>
    private static void ValidateHostPrefixCompatibility(string? name, string resolvedPath)
    {
        if (name is null || !name.StartsWith("__Host-", StringComparison.Ordinal))
        {
            return;
        }

        if (resolvedPath == "/")
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cookie '{name}' uses the '__Host-' prefix, which requires the cookie path to be '/', " +
            $"but the resolved IdentityServer cookie path is '{resolvedPath}'. This is typically caused " +
            "by a configured base path or path-base. Either remove the base path, or rename the cookie to " +
            "not use the '__Host-' prefix (a '__Secure-' prefixed name still enforces Secure but permits " +
            "non-root paths).");
    }
}

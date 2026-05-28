// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Models;

/// <summary>
/// OpenID Connect subject types.
/// </summary>
public enum SubjectTypes
{
    /// <summary>
    /// Global subject type — uses the native subject identifier as-is across all clients.
    /// </summary>
    Global = 0,

    /// <summary>
    /// Pairwise pseudonymous identifier (PPID) — the subject identifier is scoped to the client,
    /// so different clients receive different subject values for the same user.
    /// </summary>
    Ppid = 1
}

/// <summary>
/// Access token types.
/// </summary>
public enum AccessTokenType
{
    /// <summary>
    /// Self-contained JSON Web Token (JWT). The token carries all claims inline and can be validated
    /// without contacting IdentityServer. This is the default.
    /// </summary>
    Jwt = 0,

    /// <summary>
    /// Reference token. The token is an opaque handle; resource servers must call the introspection
    /// endpoint to validate it and retrieve its claims.
    /// </summary>
    Reference = 1
}

/// <summary>
/// Token usage types for refresh tokens.
/// </summary>
public enum TokenUsage
{
    /// <summary>
    /// The refresh token handle stays the same when refreshing tokens. This is the default.
    /// </summary>
    ReUse = 0,

    /// <summary>
    /// A new refresh token handle is issued every time the refresh token is used.
    /// The previous handle is invalidated.
    /// </summary>
    OneTimeOnly = 1
}

/// <summary>
/// Token expiration types for refresh tokens.
/// </summary>
public enum TokenExpiration
{
    /// <summary>
    /// Sliding expiration — when the refresh token is used, its lifetime is renewed by
    /// <c>SlidingRefreshTokenLifetime</c>. The lifetime will not exceed <c>AbsoluteRefreshTokenLifetime</c>.
    /// </summary>
    Sliding = 0,

    /// <summary>
    /// Absolute expiration — the refresh token expires at a fixed point in time determined by
    /// <c>AbsoluteRefreshTokenLifetime</c>. This is the default.
    /// </summary>
    Absolute = 1
}

/// <summary>
/// Content Security Policy Level
/// </summary>
public enum CspLevel
{
    /// <summary>
    /// CSP Level 1 — uses the <c>sandbox</c> directive for basic iframe isolation.
    /// </summary>
    One = 0,

    /// <summary>
    /// CSP Level 2 — uses the <c>frame-ancestors</c> directive for more precise embedding control.
    /// </summary>
    Two = 1
}

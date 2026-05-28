// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Common;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// AuthnStatement, Core 2.7.2
/// </summary>
public class AuthnStatement
{
    /// <summary>
    /// Authentication instant
    /// </summary>
    public DateTimeUtc AuthnInstant { get; set; }

    /// <summary>
    /// Session Index
    /// </summary>
    public string? SessionIndex { get; set; }

    /// <summary>
    /// A session established based on this assertion must expire by this time.
    /// </summary>
    public DateTimeUtc? SessionNotOnOrAfter { get; set; }

    /// <summary>
    /// Authentication Context
    /// </summary>
    public AuthnContext AuthnContext { get; set; } = default!;
}

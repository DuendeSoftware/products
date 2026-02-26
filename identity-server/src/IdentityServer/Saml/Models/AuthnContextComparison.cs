// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Specifies the comparison method to apply to requested authentication contexts.
/// SAML 2.0 Core Section 3.3.2.2.1
/// </summary>
public enum AuthnContextComparison
{
    /// <summary>
    /// The authentication context must match exactly one of the requested contexts.
    /// </summary>
    Exact,

    /// <summary>
    /// The authentication context must be at least as strong as one of the requested contexts.
    /// </summary>
    Minimum,

    /// <summary>
    /// The authentication context must be no stronger than one of the requested contexts.
    /// </summary>
    Maximum,

    /// <summary>
    /// The authentication context must be stronger than all requested contexts.
    /// </summary>
    Better
}

/// <summary>
/// Extension methods for AuthnContextComparison enum
/// </summary>
public static class AuthnContextComparisonExtensions
{
    /// <summary>
    /// Parses a string value into an AuthnContextComparison enum.
    /// Defaults to Exact if value is null, empty, or invalid.
    /// </summary>
    public static AuthnContextComparison Parse(string? value) =>
        value?.ToUpperInvariant() switch
        {
            "EXACT" => AuthnContextComparison.Exact,
            "MINIMUM" => AuthnContextComparison.Minimum,
            "MAXIMUM" => AuthnContextComparison.Maximum,
            "BETTER" => AuthnContextComparison.Better,
            null => AuthnContextComparison.Exact, // Default per SAML spec
            _ => throw new ArgumentException($"Unknown {nameof(AuthnContextComparison)}: {value}")
        };

    /// <summary>
    /// Converts an AuthnContextComparison enum to its XML attribute value.
    /// </summary>
    public static string ToAttributeValue(this AuthnContextComparison comparison) =>
        comparison switch
        {
            AuthnContextComparison.Exact => "exact",
            AuthnContextComparison.Minimum => "minimum",
            AuthnContextComparison.Maximum => "maximum",
            AuthnContextComparison.Better => "better",
            _ => "exact"
        };
}

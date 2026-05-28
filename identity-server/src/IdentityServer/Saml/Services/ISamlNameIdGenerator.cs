// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Saml.Services;

/// <summary>
/// Generates SAML NameID values for SSO responses. Register a custom implementation
/// to override the built-in email, persistent, and unspecified format handling.
/// </summary>
public interface ISamlNameIdGenerator
{
    /// <summary>
    /// Generate a NameID for the given context.
    /// </summary>
    Task<NameIdGenerationResult> GenerateAsync(NameIdGenerationContext context, Ct ct);
}

/// <summary>
/// Context provided to <see cref="ISamlNameIdGenerator"/> for NameID generation.
/// </summary>
public sealed class NameIdGenerationContext
{
    /// <summary>
    /// The authenticated subject.
    /// </summary>
    public required ClaimsPrincipal Subject { get; init; }

    /// <summary>
    /// The SAML service provider requesting the assertion.
    /// </summary>
    public required SamlServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// The resolved NameID format (after applying priority: request → SP default → unspecified).
    /// </summary>
    public required string ResolvedFormat { get; init; }

    /// <summary>
    /// SPNameQualifier from the AuthnRequest NameIDPolicy, if present.
    /// </summary>
    public string? SPNameQualifier { get; init; }
}

/// <summary>
/// Result of NameID generation, representing either a successful <see cref="NameId"/> or
/// a SAML protocol error.
/// </summary>
public sealed class NameIdGenerationResult
{
    private NameIdGenerationResult() { }

    /// <summary>
    /// The generated NameID, or null if generation failed.
    /// </summary>
    public NameId? NameId { get; private init; }

    /// <summary>
    /// The error, or null if generation succeeded.
    /// </summary>
    public SamlError? Error { get; private init; }

    /// <summary>
    /// Whether this result represents an error.
    /// </summary>
    public bool IsError => Error is not null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static NameIdGenerationResult Success(NameId nameId) => new() { NameId = nameId };

    /// <summary>
    /// Creates a failure result with SAML error status codes.
    /// </summary>
    public static NameIdGenerationResult Failure(string statusCode, string subStatusCode, string message) =>
        new()
        {
            Error = new SamlError
            {
                StatusCode = statusCode,
                SubStatusCode = subStatusCode,
                Message = message
            }
        };
}

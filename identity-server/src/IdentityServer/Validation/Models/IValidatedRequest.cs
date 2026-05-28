// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Protocol-agnostic representation of a validated request, providing
/// the common context needed by services like <see cref="IProfileService"/>.
/// Use pattern matching to downcast to a protocol-specific type
/// (e.g., <see cref="ValidatedRequest"/> for OIDC).
/// </summary>
public interface IValidatedRequest
{
    /// <summary>
    /// Gets the application that made the request.
    /// </summary>
    IConnectedApplication? Application { get; }

    /// <summary>
    /// Gets the authenticated subject.
    /// </summary>
    ClaimsPrincipal? Subject { get; }

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// Gets the issuer name for the current request. Supports multi-tenancy
    /// scenarios where different tenants have different issuer URIs.
    /// </summary>
    string IssuerName { get; }

    /// <summary>
    /// Gets the IdentityServer options in effect for the current request.
    /// </summary>
    IdentityServerOptions Options { get; }
}

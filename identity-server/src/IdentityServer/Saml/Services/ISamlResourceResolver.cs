// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.CodeAnalysis;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Saml.Services;

/// <summary>
/// Resolves the claim types that a SAML service provider is allowed to receive,
/// based on its AllowedScopes and RequestedClaimTypes configuration.
/// AllowedScopes must contain only identity resource names — API resource scopes
/// are not supported for SAML service providers.
/// </summary>
public interface ISamlResourceResolver
{
    /// <summary>
    /// Resolves the claim types for the given service provider.
    /// </summary>
    /// <param name="sp">The service provider to resolve claim types for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success with the resolved claim types, or failure with an error reason.</returns>
    Task<SamlResourceResolutionResult> ResolveRequestedClaimTypesAsync(SamlServiceProvider sp, Ct ct);
}

/// <summary>
/// The result of resolving claim types for a SAML service provider.
/// </summary>
public sealed class SamlResourceResolutionResult
{
    /// <summary>
    /// Gets whether the resolution succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded { get; private init; }

    /// <summary>
    /// Gets the resolved claim types. Only populated when <see cref="Succeeded"/> is true.
    /// </summary>
    public IReadOnlyList<string> ClaimTypes { get; private init; } = [];

    /// <summary>
    /// Gets the validated resources. Only populated when <see cref="Succeeded"/> is true.
    /// </summary>
    public ResourceValidationResult? ValidatedResources { get; private init; }

    /// <summary>
    /// Gets the error description. Only populated when <see cref="Succeeded"/> is false.
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SamlResourceResolutionResult Success(IReadOnlyList<string> claimTypes, ResourceValidationResult validatedResources) =>
        new() { Succeeded = true, ClaimTypes = claimTypes, ValidatedResources = validatedResources };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static SamlResourceResolutionResult Failure(string error) =>
        new() { Succeeded = false, Error = error };
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Services.Default;

/// <summary>
/// Default implementation of <see cref="ISamlResourceResolver"/> that resolves
/// claim types from the SP's AllowedScopes via the resource store.
/// </summary>
public sealed class DefaultSamlResourceResolver(
    IResourceStore resourceStore,
    ILogger<DefaultSamlResourceResolver> logger) : ISamlResourceResolver
{
    /// <inheritdoc/>
    public async Task<SamlResourceResolutionResult> ResolveRequestedClaimTypesAsync(SamlServiceProvider sp, Ct ct)
    {
        if (sp.AllowedScopes.Count == 0)
        {
            logger.ServiceProviderHasNoAllowedScopes(sp.EntityId);
            return SamlResourceResolutionResult.Failure("Service provider configuration error");
        }

        var resources = await resourceStore.FindEnabledResourcesByScopeAsync(sp.AllowedScopes, ct);

        // Check for scopes that didn't resolve to any identity resource
        var resolvedScopeNames = resources.IdentityResources.Select(r => r.Name).ToHashSet();
        var invalidScopes = sp.AllowedScopes.Where(s => !resolvedScopeNames.Contains(s)).ToList();

        if (invalidScopes.Count > 0)
        {
            foreach (var scope in invalidScopes)
            {
                logger.ServiceProviderHasInvalidAllowedScope(sp.EntityId, scope);
            }

            return SamlResourceResolutionResult.Failure("Service provider configuration error");
        }

        // Compute all available claim types from the resolved resources
        var allClaimTypes = resources.IdentityResources
            .SelectMany(r => r.UserClaims)
            .ToHashSet();

        // Validate that RequestedClaimTypes are within the allowed claim types
        if (sp.RequestedClaimTypes.Count > 0)
        {
            var invalidClaimTypes = sp.RequestedClaimTypes
                .Where(c => !allClaimTypes.Contains(c))
                .ToList();

            if (invalidClaimTypes.Count > 0)
            {
                foreach (var claimType in invalidClaimTypes)
                {
                    logger.ServiceProviderHasInvalidRequestedClaimType(sp.EntityId, claimType);
                }

                return SamlResourceResolutionResult.Failure("Service provider configuration error");
            }
        }

        var validatedResources = new ResourceValidationResult(resources);

        IReadOnlyList<string> claimTypes = sp.RequestedClaimTypes.Count > 0
            ? [.. sp.RequestedClaimTypes]
            : [.. allClaimTypes];

        return SamlResourceResolutionResult.Success(claimTypes, validatedResources);
    }
}

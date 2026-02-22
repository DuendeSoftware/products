// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates requested resources (scopes and resource indicators)
/// </summary>
public interface IResourceValidator
{
    // todo: should this be used anywhere we re-create tokens? do we need to re-run scope validation?

    /// <summary>
    /// Validates the requested resources for the client.
    /// </summary>
    /// <param name="request">The resource validation request.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<ResourceValidationResult> ValidateRequestedResourcesAsync(ResourceValidationRequest request, Ct ct);
}

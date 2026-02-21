// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;

namespace Duende.IdentityServer.Configuration.Validation.DynamicClientRegistration;

/// <summary>
/// Validates a dynamic client registration request.
/// </summary>
public interface IDynamicClientRegistrationValidator
{
    /// <summary>
    /// Validates a dynamic client registration request. 
    /// </summary>
    /// <param name="context">Contextual information about the DCR
    /// request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that returns an <see
    /// cref="IDynamicClientRegistrationValidationResult"/>, which either
    /// indicates success or failure.</returns>
    Task<IDynamicClientRegistrationValidationResult> ValidateAsync(DynamicClientRegistrationContext context, CT ct);
}

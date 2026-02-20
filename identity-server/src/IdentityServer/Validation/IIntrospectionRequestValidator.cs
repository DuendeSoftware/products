// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Interface for the introspection request validator
/// </summary>
public interface IIntrospectionRequestValidator
{
    /// <summary>
    /// Validates the request.
    /// </summary>
    Task<IntrospectionRequestValidationResult> ValidateAsync(IntrospectionRequestValidationContext context, CT ct);
}

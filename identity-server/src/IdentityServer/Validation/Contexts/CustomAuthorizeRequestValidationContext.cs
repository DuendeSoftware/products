// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context for custom authorize request validation.
/// </summary>
public class CustomAuthorizeRequestValidationContext
{
    /// <summary>
    /// The result of custom validation. 
    /// </summary>
    public AuthorizeRequestValidationResult? Result { get; set; }
}

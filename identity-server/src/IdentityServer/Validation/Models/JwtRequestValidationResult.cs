// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models the result of JWT request validation.
/// </summary>
public class JwtRequestValidationResult : ValidationResult
{
    /// <summary>
    /// The key/value pairs from the JWT payload of a successfully validated
    /// request, or null if a validation error occurred.
    /// </summary>
    public IEnumerable<Claim>? Payload { get; set; }
}

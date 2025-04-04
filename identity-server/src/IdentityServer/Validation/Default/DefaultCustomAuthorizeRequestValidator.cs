// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default custom request validator
/// </summary>
internal class DefaultCustomAuthorizeRequestValidator : ICustomAuthorizeRequestValidator
{
    /// <summary>
    /// Custom validation logic for the authorize request.
    /// </summary>
    /// <param name="context">The context.</param>
    public Task ValidateAsync(CustomAuthorizeRequestValidationContext context) => Task.CompletedTask;
}

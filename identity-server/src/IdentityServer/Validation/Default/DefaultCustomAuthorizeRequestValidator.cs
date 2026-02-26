// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default custom request validator
/// </summary>
internal class DefaultCustomAuthorizeRequestValidator : ICustomAuthorizeRequestValidator
{
    /// <inheritdoc/>
    public Task ValidateAsync(CustomAuthorizeRequestValidationContext context, Ct _) => Task.CompletedTask;
}

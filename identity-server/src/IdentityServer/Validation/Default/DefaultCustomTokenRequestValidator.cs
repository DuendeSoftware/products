// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default custom request validator
/// </summary>
internal class DefaultCustomTokenRequestValidator : ICustomTokenRequestValidator
{
    /// <inheritdoc/>
    public Task ValidateAsync(CustomTokenRequestValidationContext context, Ct _) => Task.CompletedTask;
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// No-op client configuration validator (for backwards-compatibility).
/// </summary>
/// <seealso cref="IClientConfigurationValidator" />
public class NopClientConfigurationValidator : IClientConfigurationValidator
{
    /// <inheritdoc/>
    public Task ValidateAsync(ClientConfigurationValidationContext context, Ct _)
    {
        context.IsValid = true;
        return Task.CompletedTask;
    }
}

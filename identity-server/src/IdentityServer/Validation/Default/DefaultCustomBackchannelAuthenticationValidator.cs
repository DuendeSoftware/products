// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default implementation of the CIBA validator extensibility point. This
/// validator deliberately does nothing.
/// </summary>
public class DefaultCustomBackchannelAuthenticationValidator : ICustomBackchannelAuthenticationValidator
{
    /// <inheritdoc/>
    /// <inheritdoc/>
    public Task ValidateAsync(CustomBackchannelAuthenticationRequestValidationContext customValidationContext, Ct ct) => Task.CompletedTask;
}

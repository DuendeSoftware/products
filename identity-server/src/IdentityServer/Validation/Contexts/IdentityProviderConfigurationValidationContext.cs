// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context for identity provider configuration validation.
/// </summary>
public class IdentityProviderConfigurationValidationContext : IdentityProviderConfigurationValidationContext<IdentityProvider>
{
    /// <summary>
    /// Initializes a new instance of the IdentityProviderConfigurationValidationContext class.
    /// </summary>
    public IdentityProviderConfigurationValidationContext(IdentityProvider idp) : base(idp)
    {
    }
}

/// <summary>
/// Context for identity provider configuration validation.
/// </summary>
public class IdentityProviderConfigurationValidationContext<T>
    where T : IdentityProvider
{
    /// <summary>
    /// Gets or sets the identity provider.
    /// </summary>
    public T IdentityProvider { get; }

    /// <summary>
    /// Returns true if the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the IdentityProviderConfigurationValidationContext class.
    /// </summary>
    public IdentityProviderConfigurationValidationContext(T idp) => IdentityProvider = idp;

    /// <summary>
    /// Sets a validation error.
    /// </summary>
    /// <param name="message">The message.</param>
    public void SetError(string message)
    {
        IsValid = false;
        ErrorMessage = message;
    }
}

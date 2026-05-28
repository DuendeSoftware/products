// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context for SAML service provider configuration validation.
/// </summary>
public class SamlServiceProviderConfigurationValidationContext
{
    /// <summary>
    /// Gets the service provider being validated.
    /// </summary>
    public SamlServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Returns true if the service provider configuration is valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamlServiceProviderConfigurationValidationContext"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public SamlServiceProviderConfigurationValidationContext(SamlServiceProvider serviceProvider) =>
        ServiceProvider = serviceProvider;

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

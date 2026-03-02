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
    public SamlServiceProvider ServiceProvider { get; }
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public SamlServiceProviderConfigurationValidationContext(SamlServiceProvider serviceProvider)
        => ServiceProvider = serviceProvider;

    public void SetError(string message)
    {
        IsValid = false;
        ErrorMessage = message;
    }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for invalid SAML service provider configuration.
/// </summary>
public class InvalidSamlServiceProviderConfigurationEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidSamlServiceProviderConfigurationEvent"/> class.
    /// </summary>
    /// <param name="serviceProvider">The SAML service provider.</param>
    /// <param name="errorMessage">The error message.</param>
    public InvalidSamlServiceProviderConfigurationEvent(SamlServiceProvider serviceProvider, string errorMessage)
        : base(EventCategories.Error,
            "Invalid SAML Service Provider Configuration",
            EventTypes.Error,
            EventIds.InvalidSamlServiceProviderConfiguration,
            errorMessage)
    {
        EntityId = serviceProvider.EntityId;
        DisplayName = serviceProvider.DisplayName ?? "unknown name";
    }

    /// <summary>
    /// Gets or sets the entity ID of the SAML service provider.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the SAML service provider.
    /// </summary>
    public string DisplayName { get; set; }
}

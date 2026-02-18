// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Internal.Saml.Metadata.Models;

/// <summary>
/// Represents a SAML entity descriptor that describes a SAML entity (IdP or SP).
/// </summary>
internal record EntityDescriptor
{
    /// <summary>
    /// Gets or sets the entity ID (typically the IdP issuer URI).
    /// This uniquely identifies the SAML entity.
    /// </summary>
    internal required string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the IdP SSO descriptor.
    /// Contains the Identity Provider's SSO configuration and capabilities.
    /// </summary>
    internal IdpSsoDescriptor? IdpSsoDescriptor { get; set; }

    /// <summary>
    /// Gets or sets the validity period end time (optional).
    /// If set, indicates when this metadata expires.
    /// </summary>
    internal DateTime? ValidUntil { get; set; }
}

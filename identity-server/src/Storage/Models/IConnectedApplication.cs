// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Represents a registered application (OIDC client or SAML Service Provider) in a protocol-agnostic way.
/// </summary>
public interface IConnectedApplication
{
    /// <summary>
    /// Gets the unique identifier for the application (e.g., ClientId for OIDC, EntityId for SAML).
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Gets the display name of the application.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Gets the description of the application.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets whether the application is enabled.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Gets the protocol type (e.g., "oidc", "saml2p").
    /// </summary>
    string ProtocolType { get; }

    /// <summary>
    /// Gets whether user consent is required.
    /// </summary>
    bool RequireConsent { get; }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Metadata.Models;

/// <summary>
/// Describes a SAML Identity Provider's SSO capabilities.
/// This element contains all the information needed for a Service Provider
/// to interact with the Identity Provider.
/// </summary>
internal record IdpSsoDescriptor
{
    /// <summary>
    /// Gets or sets the protocol support enumeration.
    /// Typically "urn:oasis:names:tc:SAML:2.0:protocol".
    /// Indicates which SAML protocols this IdP supports.
    /// </summary>
    internal required string ProtocolSupportEnumeration { get; set; }

    /// <summary>
    /// Gets or sets whether the IdP requires authentication requests to be signed.
    /// </summary>
    internal bool WantAuthnRequestsSigned { get; set; }

    /// <summary>
    /// Gets or sets the signing certificates.
    /// Contains the public keys used to verify signatures from this IdP.
    /// </summary>
    internal Collection<KeyDescriptor> KeyDescriptors { get; init; } = [];

    /// <summary>
    /// Gets or sets the supported NameID formats.
    /// Indicates which name identifier formats this IdP can provide.
    /// </summary>
    internal Collection<string> NameIdFormats { get; init; } = [];

    /// <summary>
    /// Gets or sets the SingleSignOnService endpoints.
    /// Defines where and how Service Providers can initiate SSO.
    /// </summary>
    internal Collection<SingleSignOnService> SingleSignOnServices { get; init; } = [];

    /// <summary>
    /// Gets or sets the SingleLogoutService endpoints.
    /// Defines where and how Service Providers can send logout requests.
    /// </summary>
    internal Collection<SingleLogoutService> SingleLogoutServices { get; init; } = [];
}

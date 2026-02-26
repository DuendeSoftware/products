// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Internal.Saml.Metadata.Models;

/// <summary>
/// Describes a SAML SingleLogoutService endpoint.
/// Specifies where and how a Service Provider can send logout requests.
/// </summary>
internal record SingleLogoutService
{
    /// <summary>
    /// Gets or sets the binding (HTTP-Redirect, HTTP-POST, etc.).
    /// Indicates the protocol binding to use for this endpoint.
    /// </summary>
    internal required SamlBinding Binding { get; set; }

    /// <summary>
    /// Gets or sets the location URI.
    /// The endpoint URI where logout requests should be sent.
    /// </summary>
    internal required Uri Location { get; set; }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents a SAML 2.0 AttributeStatement element
/// </summary>
internal record AttributeStatement
{
    /// <summary>
    /// Attributes in this statement
    /// </summary>
    public List<SamlAttribute> Attributes { get; set; } = [];
}

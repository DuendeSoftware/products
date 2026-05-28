// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Common;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// A Saml assertion
/// </summary>
public class Assertion
{
    /// <summary>
    /// Id of the assertion
    /// </summary>
    public string Id { get; set; } = XmlHelpers.CreateId();

    /// <summary>
    /// Issuer of the assertion.
    /// </summary>
    public NameId Issuer { get; set; } = default!;

    /// <summary>
    /// Version. Must be 2.0
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    /// Isssue instant of the assertion
    /// </summary>
    public DateTimeUtc IssueInstant { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Subject of the assertion
    /// </summary>
    public Subject? Subject { get; set; }

    /// <summary>
    /// Conditions of the assertion
    /// </summary>
    public Conditions? Conditions { get; set; }

    /// <summary>
    /// Authentication Statement
    /// </summary>
    public AuthnStatement? AuthnStatement { get; set; }

    /// <summary>
    /// Attributes
    /// </summary>
    /// <remarks>
    /// Saml2 Core 2.7.3 requires an AttributeStatement to have at least one Attribute,
    /// if there are no attributes, then the entire AttributeStatement should be omitted.
    /// But, in C# it's more convenient to have an empty list instead of null. So we do
    /// that on the C# side, but check on serialization.
    /// </remarks>
    public AttributeStatement Attributes { get; init; } = [];

    /// <summary>
    /// Trust level derived from the signature validation
    /// </summary>
    public TrustLevel TrustLevel { get; set; }
}

// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

internal record Assertion
{
    // ev: This can also be a UUIDV7

    /// <summary>
    /// Unique identifier for this assertion
    /// Must start with a _ character and be unique
    ///
    /// According to SAML 2.0 Core Specification (Section 1.3.4):
    ///- ID attributes must be of type xs:ID
    ///- xs:ID must conform to the NCName production (Non-Colonized Name) from the XML Namespaces specification
    ///- NCName cannot start with a digit, colon, or certain other characters
    /// </summary>
    public string Id { get; } = SamlIds.NewAssertionId();

    /// <summary>
    /// SAML version (must be "2.0")
    /// </summary>
    public string Version { get; } = SamlVersions.V2;

    /// <summary>
    /// Time instant of issuance
    /// </summary>
    public required DateTime IssueInstant { get; set; }

    /// <summary>
    /// Identifies the entity that issued the assertion
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// The subject of the assertion
    /// </summary>
    public Subject? Subject { get; set; }

    /// <summary>
    /// Conditions under which the assertion is valid
    /// </summary>
    public Conditions? Conditions { get; set; }

    /// <summary>
    /// Authentication statements
    /// </summary>
    public List<AuthnStatement> AuthnStatements { get; set; } = [];

    /// <summary>
    /// Attribute statements
    /// </summary>
    public List<AttributeStatement> AttributeStatements { get; set; } = [];

    /// <summary>
    /// Authorization decision statements
    /// </summary>
    public List<AuthzDecisionStatement> AuthzDecisionStatements { get; set; } = [];
}

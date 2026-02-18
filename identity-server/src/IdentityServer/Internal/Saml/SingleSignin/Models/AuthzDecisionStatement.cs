// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents a SAML 2.0 AuthzDecisionStatement element (Section 2.7.4)
/// </summary>
internal record AuthzDecisionStatement
{
    /// <summary>
    /// URI reference identifying the resource to which access authorization is sought
    /// </summary>
    public required string Resource { get; set; }

    /// <summary>
    /// The decision rendered by the SAML authority with respect to the specified resource
    /// </summary>
    public DecisionType Decision { get; set; }

    /// <summary>
    /// A set of assertions that the SAML authority relied on in making the decision (optional)
    /// </summary>
    public Evidence? Evidence { get; set; }
}

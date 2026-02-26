// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents evidence supporting the authorization decision (optional)
/// </summary>
internal record Evidence
{
    /// <summary>
    /// URI references to assertions
    /// </summary>
    public List<string> AssertionIDRefs { get; set; } = [];

    /// <summary>
    /// URI references to assertions
    /// </summary>
    public List<string> AssertionURIRefs { get; set; } = [];

    /// <summary>
    /// Embedded assertions
    /// </summary>
    public List<Assertion> Assertions { get; set; } = [];
}

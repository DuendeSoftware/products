// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// A Saml2p SamlResponse
/// </summary>
public class Response : StatusResponseType
{
    /// <summary>
    /// Assertions
    /// </summary>
    public List<Assertion> Assertions { get; } = [];
}

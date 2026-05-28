// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
///  Specifies the identity providers trusted by the requester.
///  Element Scoping, Core 3.4.1.2
/// </summary>
public class Scoping
{
    /// <summary>
    /// Specifies a number of proxying indirections permissable between
    /// the identity provider that receives this AuthRequest
    /// and the identity provider that ultimately authenticates the principal.
    /// </summary>
    public int? ProxyCount { get; set; }

    /// <summary>
    /// An advisory list of identity providers and associated information.
    /// </summary>
    public IdpList? IDPList { get; set; }

    /// <summary>
    /// Requesting entities are identified on whose behalf the requester is acting.
    /// </summary>
    public List<string> RequesterID { get; } = [];
}

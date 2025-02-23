// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the type of proof of possession
/// </summary>
public enum ProofType
{
    /// <summary>
    /// None
    /// </summary>
    None,
    /// <summary>
    /// Client certificate
    /// </summary>
    ClientCertificate,
    /// <summary>
    /// DPoP
    /// </summary>
    DPoP
}

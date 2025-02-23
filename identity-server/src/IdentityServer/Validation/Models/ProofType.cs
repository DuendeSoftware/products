// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models the thumbprint of a proof key
/// </summary>
public class ProofKeyThumbprint
{
    /// <summary>
    /// The type
    /// </summary>
    public ProofType Type { get; set; }
    /// <summary>
    /// The thumbprint value used in a cnf thumbprint claim value
    /// </summary>
    public string Thumbprint { get; set; }
}

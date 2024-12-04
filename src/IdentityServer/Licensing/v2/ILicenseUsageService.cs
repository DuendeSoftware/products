// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// Tracks the usage of the license. 
/// </summary>
public interface ILicenseUsageService
{
    /// <summary>
    /// Gets the licensed features that have been used.
    /// </summary>
    HashSet<LicenseFeature> UsedFeatures { get; }
    /// <summary>
    /// Indicates that a licensed feature has been used.
    /// </summary>
    void UseFeature(LicenseFeature feature);

    /// <summary>
    /// Gets the client ids that have been used.
    /// </summary>
    HashSet<string> UsedClients { get; }
    /// <summary>
    /// Indicates that a client id has been used.
    /// </summary>
    void UseClient(string clientId);

    
    /// <summary>
    /// Gets the issuers that have been used.
    /// </summary>
    HashSet<string> UsedIssuers { get; }
    /// <summary>
    /// Indicates that an issuer has been used.
    /// </summary>
    void UseIssuer(string issuer);
}
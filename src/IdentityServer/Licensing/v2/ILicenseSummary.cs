// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// Summarizes the usage of IdentityServer
/// </summary>
public interface ILicenseSummary
{
    /// <summary>
    /// Summarizes the usage of IdentityServer, including licensed features, clients, and issuers.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the license edition.
    /// </summary>
    public string LicenseEdition { get; }

    /// <summary>
    /// Gets the licensed enterprise edition features that have been used.
    /// </summary>
    IEnumerable<string> EnterpriseFeaturesUsed { get; }
    
    /// <summary>
    /// Gets the licensed business edition features that have been used.
    /// </summary>
    IEnumerable<string> BusinessFeaturesUsed { get; }

    /// <summary>
    /// Gets other licensed features that have been used.
    /// </summary>
    IEnumerable<string> OtherFeaturesUsed { get; }

    /// <summary>
    /// Gets the client ids that have been used.
    /// </summary>
    IEnumerable<string> UsedClients { get; }
    
    /// <summary>
    /// Gets the issuers that have been used.
    /// </summary>
    IEnumerable<string> UsedIssuers { get; }
}

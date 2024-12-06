// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// Summarizes the usage of IdentityServer
/// </summary>
public interface IUsageSummary
{
    /// <summary>
    /// Gets the license edition.
    /// </summary>
    public string LicenseEdition { get; }

    /// <summary>
    /// Gets the licensed features that have been used.
    /// </summary>
    IEnumerable<string> FeaturesUsed { get; }

    /// <summary>
    /// Gets the client ids that have been used.
    /// </summary>
    IEnumerable<string> UsedClients { get; }
    
    /// <summary>
    /// Gets the issuers that have been used.
    /// </summary>
    IEnumerable<string> UsedIssuers { get; }
}

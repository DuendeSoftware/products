// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents SAML 2.0 Conditions element
/// </summary>
internal record Conditions
{
    /// <summary>
    /// Time instant before which the assertion is invalid
    /// </summary>
    public DateTime? NotBefore { get; set; }

    /// <summary>
    /// Time instant at which the assertion expires
    /// </summary>
    public DateTime? NotOnOrAfter { get; set; }

    /// <summary>
    /// Audience restrictions for the assertion
    /// </summary>
    public ReadOnlyCollection<string> AudienceRestrictions { get; init; } = new List<string>().AsReadOnly();
}

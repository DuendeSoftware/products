// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents a SAML 2.0 Subject element
/// </summary>
internal record Subject
{
    /// <summary>
    /// The name identifier of the subject
    /// </summary>
    public NameIdentifier? NameId { get; set; }

    /// <summary>
    /// Subject confirmation data
    /// </summary>
    public ReadOnlyCollection<SubjectConfirmation> SubjectConfirmations { get; init; } = new List<SubjectConfirmation>().AsReadOnly();
}

/// <summary>
/// Represents a SAML 2.0 SubjectConfirmation element
/// </summary>
internal record SubjectConfirmation
{
    /// <summary>
    /// The method used to confirm the subject (URI)
    /// </summary>
    public required string Method { get; set; }

    /// <summary>
    /// Subject confirmation data
    /// </summary>
    public SubjectConfirmationData? Data { get; set; }
}

/// <summary>
/// Represents SAML 2.0 SubjectConfirmationData element
/// </summary>
internal record SubjectConfirmationData
{
    /// <summary>
    /// Time instant before which the subject cannot be confirmed
    /// </summary>
    public DateTime? NotBefore { get; set; }

    /// <summary>
    /// Time instant at which the subject can no longer be confirmed
    /// </summary>
    public DateTime? NotOnOrAfter { get; set; }

    /// <summary>
    /// URI of a recipient entity
    /// </summary>
    public Uri? Recipient { get; set; }

    /// <summary>
    /// ID of a SAML request to which this is a response
    /// </summary>
    public string? InResponseTo { get; set; }
}
